using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Governance;

/// <summary>
/// Rapporte les opérations qui n'emploient pas ce que l'équipe s'est donné pour standard.
/// </summary>
/// <remarks>
/// Le standard vient du jeu de règles, jamais de l'outil : les en-têtes qu'une maison exige et les codes
/// d'erreur qu'elle publie ne se devinent pas. Ce qui est signalé est donc un écart à une décision écrite,
/// et rien d'autre.
/// <para>
/// Un constat par opération, et non un par manquement. Sur une description de mille opérations, la
/// seconde forme produirait quatre mille lignes pour la même décision — adopter le standard, ou le
/// changer.
/// </para>
/// </remarks>
public sealed class MissingStandardsRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public MissingStandardsRule()
        : base("AUD-300", SpecObjectKind.Operation)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        bool globalSecurity = Nodes.Sequence(subject.Root, "security") is { Items.Count: > 0 };
        List<Finding> findings = [];

        foreach (AuditNode operation in subject.Nodes(SpecObjectKind.Operation))
        {
            IReadOnlyList<string> departures = [.. Departures(subject, operation, globalSecurity)];

            if (departures.Count == 0)
            {
                continue;
            }

            findings.Add(Report(
                "L'opération n'emploie pas tout le standard : " + string.Join(" ; ", departures) + ".",
                operation.Location,
                "L'y conformer, ou retirer du jeu de règles ce qui n'est plus le standard."));
        }

        return RuleReport.Evaluated(findings);
    }

    /// <summary>Ce qui manque à une opération pour être conforme.</summary>
    private static IEnumerable<string> Departures(
        AuditSubject subject,
        AuditNode operation,
        bool globalSecurity)
    {
        if (Headers(subject, operation) is { Count: > 0 } headers)
        {
            yield return "en-têtes absents : " + string.Join(", ", headers);
        }

        if (Responses(subject, operation) is { Count: > 0 } responses)
        {
            yield return "réponses absentes : " + string.Join(", ", responses);
        }

        if (Nodes.Items(operation.Node, "tags").Count == 0)
        {
            yield return "aucune étiquette";
        }

        if (!globalSecurity && Nodes.Sequence(operation.Node, "security") is not { Items.Count: > 0 })
        {
            yield return "aucune sécurité, ni la sienne ni celle du document";
        }
    }

    /// <summary>Les en-têtes standard que l'opération ne porte pas.</summary>
    private static IReadOnlyList<string> Headers(AuditSubject subject, AuditNode operation)
    {
        HashSet<string> carried = new(
            Nodes.Items(operation.Node, "parameters")
                .Select(parameter => Nodes.Text(parameter, "name"))
                .OfType<string>(),
            StringComparer.OrdinalIgnoreCase);

        return [.. subject.Ruleset.StandardHeaders.Where(header => !carried.Contains(header))];
    }

    /// <summary>Les réponses standard que l'opération ne déclare pas.</summary>
    private static IReadOnlyList<string> Responses(AuditSubject subject, AuditNode operation)
    {
        if (Nodes.Map(operation.Node, "responses") is not { } responses)
        {
            return [];
        }

        HashSet<string> declared = new(
            responses.Entries.Select(entry => entry.Key.Resolved.Canonical), StringComparer.Ordinal);

        return [.. subject.Ruleset.StandardResponses.Where(code => !declared.Contains(code))];
    }
}
