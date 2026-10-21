using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les propriétés qui ressemblent à des dates et ne déclarent aucun format.
/// </summary>
/// <remarks>
/// Une déduction, non un fait, et le constat le dit : le nom d'une propriété n'est pas une preuve de son
/// contenu. Elle vaut d'être faite parce qu'une date sans format laisse chaque client inventer la sienne,
/// et que le jour où deux clients n'inventent pas la même, personne ne saura d'où vient l'écart.
/// </remarks>
public sealed class DateFormatRule : AuditRule
{
    private static readonly string[] Hints =
        ["date", "datetime", "time", "horodat", "timestamp", "instant"];

    private static readonly string[] Suffixes = ["Le", "At", "On"];

    /// <summary>Crée la règle.</summary>
    public DateFormatRule()
        : base("AUD-206", SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. subject.Nodes(SpecObjectKind.Schema)
                .Select(schema => Nodes.Map(schema.Node, "properties"))
                .OfType<MappingNode>()
                .SelectMany(properties => properties.Entries)
                .Where(property => Suspicious(property.Key.Text))
                .Where(property => Undated(property.Value))
                .Select(property => Suggestion(property.Key.Text, property.Value.Location)),
        ]);
    }

    /// <summary>Indique si le nom d'une propriété évoque une date ou un instant.</summary>
    private static bool Suspicious(string name)
    {
        string lowered = name.ToLowerInvariant();

        // Le suffixe est cherché avec sa capitale : « creeLe » évoque une date, « libelle » ne fait que
        // se terminer par les mêmes lettres. Sans la casse, la règle signalerait un mot sur trois.
        return Hints.Any(hint => lowered.Contains(hint, StringComparison.Ordinal))
            || Suffixes.Any(suffix =>
                name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length + 2);
    }

    /// <summary>Indique si la propriété est une chaîne sans format déclaré.</summary>
    private static bool Undated(SpecNode property) =>
        string.Equals(Nodes.Text(property, "type"), "string", StringComparison.Ordinal)
        && !Nodes.Has(property, "format")
        && !Nodes.Has(property, "pattern")
        && !Nodes.IsReference(property);

    private Finding Suggestion(string name, SpecPointer location) =>
        Report(
            $"La propriété « {name} » est une chaîne sans format, alors que son nom évoque une date.",
            location,
            "Déclarer « format: date » ou « format: date-time », si c'en est une.") with
        {
            Evidence = new FindingEvidence(
                Reasoning: "Déduit du nom de la propriété, qui n'établit pas son contenu."),
        };
}
