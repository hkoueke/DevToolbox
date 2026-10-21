using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les composants qu'aucun chemin n'atteint.
/// </summary>
/// <remarks>
/// La question n'est pas « qui me référence » mais « m'atteint-on depuis un chemin ». Deux composants
/// inaccessibles qui se référencent l'un l'autre ont chacun un référent, et aucun des deux ne sert : seule
/// une accessibilité calculée depuis les racines du document les trouve.
/// <para>
/// La suppression n'est prouvée sûre que si l'analyse des références est complète. Une référence non
/// résolue ou préservée hors du document laisse ouverte la possibilité qu'un composant soit atteint
/// d'ailleurs, et le constat passe alors en relecture.
/// </para>
/// </remarks>
public sealed class UnusedComponentRule : AuditRule
{
    private const string ComponentsPrefix = "/components/";

    /// <summary>Crée la règle.</summary>
    public UnusedComponentRule()
        : base("AUD-003", SpecObjectKind.Components)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.References is not { } references)
        {
            return RuleReport.NotEvaluated(
                "Les références n'ont pas été résolues : aucun composant ne peut être dit inaccessible.");
        }

        bool complete = !references.Unresolved.Any() && !references.ExternalPreserved.Any();
        HashSet<string> reachable = Reachable(references, Required(subject));

        return RuleReport.Evaluated(
        [
            .. Declared(subject)
                .Where(component => !reachable.Contains(component.Pointer))
                .Where(component => !Kept(subject, component.Name))
                .Select(component => Unreachable(component, complete)),
        ]);
    }

    /// <summary>Les composants que le document déclare, avec leur emplacement.</summary>
    private static IEnumerable<(string Pointer, string Name, SpecNode Node)> Declared(
        AuditSubject subject)
    {
        foreach (AuditNode node in subject.Nodes(SpecObjectKind.Components))
        {
            if (node.Mapping is not { } components)
            {
                continue;
            }

            foreach (MappingEntry family in components.Entries)
            {
                if (family.Value is not MappingNode members)
                {
                    continue;
                }

                foreach (MappingEntry member in members.Entries)
                {
                    yield return (
                        ComponentsPrefix + family.Key.Text + "/" + member.Key.Text,
                        member.Key.Text,
                        member.Value);
                }
            }
        }
    }

    /// <summary>
    /// Les mécanismes de sécurité qu'une exigence nomme.
    /// </summary>
    /// <remarks>
    /// Une exigence de sécurité n'est pas une référence : elle nomme un mécanisme par sa clé. Le graphe
    /// des références ne la voit donc pas, et un mécanisme employé par tout le document passerait pour
    /// inaccessible — le genre de constat qui discrédite un audit entier.
    /// </remarks>
    private static IEnumerable<string> Required(AuditSubject subject) =>
        Nodes.Items(subject.Root, "security")
            .Concat(subject.Nodes(SpecObjectKind.Operation)
                .SelectMany(operation => Nodes.Items(operation.Node, "security")))
            .OfType<MappingNode>()
            .SelectMany(requirement => requirement.Entries)
            .Select(entry => ComponentsPrefix + "securitySchemes/" + entry.Key.Text);

    /// <summary>Les composants atteints depuis l'extérieur de <c>components</c>, transitivement.</summary>
    private static HashSet<string> Reachable(ReferenceGraphResult references, IEnumerable<string> roots)
    {
        Dictionary<string, List<string>> outgoing = new(StringComparer.Ordinal);
        Queue<string> pending = new();

        foreach (string root in roots)
        {
            pending.Enqueue(root);
        }

        foreach (ReferenceEdge edge in references.Edges
            .Where(edge => edge.Resolution == ReferenceResolution.Resolved))
        {
            if (edge.ResolvedLocation is null
                || CarriedObject.Of(edge.ResolvedLocation) is not { } target
                || !target.StartsWith(ComponentsPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string? owner = CarriedObject.Of(edge.From);

            if (owner is not null && owner.StartsWith(ComponentsPrefix, StringComparison.Ordinal))
            {
                if (!outgoing.TryGetValue(owner, out List<string>? targets))
                {
                    targets = [];
                    outgoing[owner] = targets;
                }

                targets.Add(target);
                continue;
            }

            pending.Enqueue(target);
        }

        HashSet<string> reachable = new(StringComparer.Ordinal);

        while (pending.Count > 0)
        {
            string component = pending.Dequeue();

            if (!reachable.Add(component)
                || !outgoing.TryGetValue(component, out List<string>? next))
            {
                continue;
            }

            foreach (string target in next)
            {
                pending.Enqueue(target);
            }
        }

        return reachable;
    }

    /// <summary>Indique si un motif de conservation apparie le nom.</summary>
    private static bool Kept(AuditSubject subject, string name) =>
        subject.Ruleset.KeepComponentPatterns.Any(pattern =>
            pattern.EndsWith('*')
                ? name.StartsWith(pattern[..^1], StringComparison.Ordinal)
                : string.Equals(name, pattern, StringComparison.Ordinal));

    private Finding Unreachable(
        (string Pointer, string Name, SpecNode Node) component,
        bool complete) =>
        Report(
            $"Le composant « {component.Name} » n'est atteint depuis aucun chemin.",
            component.Node.Location,
            complete
                ? "Le retirer, ou le déclarer à conserver."
                : "Vérifier qu'aucun document extérieur ne l'emploie avant de le retirer.",
            safety: complete ? SafetyClass.Safe : SafetyClass.ReviewRequired) with
        {
            StrictEquivalenceEstablished = complete,
            EstimatedBytesSaved = component.Node.Span.Length,
            Evidence = new FindingEvidence(
                Reasoning: complete
                    ? null
                    : "Une référence non résolue ou préservée hors du document laisse l'analyse incomplète."),
        };
}
