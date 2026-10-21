using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Governance;

/// <summary>
/// Rapporte un même en-tête exigé par certaines opérations et facultatif pour d'autres.
/// </summary>
/// <remarks>
/// C'est le défaut que personne ne trouve en relisant un fichier, parce qu'il n'est visible qu'en
/// comparant des endroits éloignés. Pour l'appelant, il se traduit par un en-tête qui marche partout sauf
/// à un endroit — ou l'inverse, plus désagréable encore : un en-tête qu'on croit facultatif et qui ne
/// l'est pas.
/// </remarks>
public sealed class InconsistentHeaderRequirednessRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public InconsistentHeaderRequirednessRule()
        : base("AUD-301", SpecObjectKind.Operation)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. Headers(subject)
                .GroupBy(header => header.Name, StringComparer.Ordinal)
                .Where(group => group.Select(header => header.Required).Distinct().Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Divergent(group.Key, [.. group])),
        ]);
    }

    /// <summary>Les en-têtes déclarés par les opérations, avec leur caractère obligatoire.</summary>
    private static IEnumerable<(string Name, bool Required, SpecPointer Location)> Headers(
        AuditSubject subject) =>
        subject.Nodes(SpecObjectKind.Operation)
            .SelectMany(operation => Nodes.Items(operation.Node, "parameters"))
            .Select(parameter => Nodes.Resolve(subject.Root, parameter))
            .OfType<SpecNode>()
            .Where(parameter => string.Equals(
                Nodes.Text(parameter, "in"), "header", StringComparison.Ordinal))
            .Select(parameter => (
                Name: Nodes.Text(parameter, "name"),
                Required: Nodes.Flag(parameter, "required") == true,
                parameter.Location))
            .Where(header => header.Name is not null)
            .Select(header => (header.Name!, header.Required, header.Location));

    private Finding Divergent(
        string name,
        IReadOnlyList<(string Name, bool Required, SpecPointer Location)> group)
    {
        int required = group.Count(header => header.Required);

        return Report(
            $"L'en-tête « {name} » est exigé par {required} opération(s) et facultatif pour "
                + $"{group.Count - required}.",
            group[0].Location,
            "Trancher : le même en-tête ne peut pas être obligatoire selon l'opération sans que "
                + "l'appelant ait à le savoir opération par opération.") with
        {
            Locations = [.. group.Select(header => header.Location)],
            Evidence = new FindingEvidence(Occurrences: group.Count),
        };
    }
}
