using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte chaque référence qui ne désigne rien, et chacun des endroits qui l'emploie.
/// </summary>
/// <remarks>
/// Chacun des endroits, et non le premier : une référence morte écrite à vingt endroits est un seul défaut
/// mais vingt corrections, et nommer un seul emplacement obligerait le lecteur à chercher les dix-neuf
/// autres.
/// </remarks>
public sealed class UnresolvedReferenceRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public UnresolvedReferenceRule()
        : base("AUD-002", SpecObjectKind.Document)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.References is not { } references)
        {
            return RuleReport.NotEvaluated(
                "Les références n'ont pas été résolues : la règle ne peut pas dire qu'elles aboutissent.");
        }

        IReadOnlyList<Finding> found =
        [
            .. references.Unresolved
                .GroupBy(edge => edge.To.ToString(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Dangling(group.Key, [.. group])),
        ];

        // Les constats trouvés sont vrais, et sont rendus. Mais le silence de cette règle se lit
        // « aucune référence morte », et sur une traversée arrêtée par un budget ce serait faux : ce qui
        // n'a pas été atteint n'est ni mort ni vivant. D'où le rapport tronqué — il garde ce qui a été
        // vu sans laisser conclure sur ce qui ne l'a pas été.
        return references.Exhausted
            ? RuleReport.Truncated(
                found,
                "La traversée des références s'est arrêtée sur un budget : les références situées "
                + "au-delà n'ont pas été vérifiées.")
            : RuleReport.Evaluated(found);
    }

    private Finding Dangling(string target, IReadOnlyList<ReferenceEdge> uses)
    {
        IReadOnlyList<SpecPointer> locations = [.. uses.Select(edge => edge.From)];

        return Report(
            uses.Count == 1
                ? $"La référence « {target} » ne désigne rien."
                : $"La référence « {target} » ne désigne rien, et est employée {uses.Count} fois.",
            locations[0],
            "Rétablir la cible, ou corriger la référence.") with
        {
            Locations = locations,
            Evidence = new FindingEvidence(Occurrences: uses.Count),
        };
    }
}
