using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les en-têtes qui ne diffèrent que par la casse, sans jamais proposer de les réunir.
/// </summary>
/// <remarks>
/// HTTP veut que la casse d'un nom d'en-tête soit sans importance, et c'est vrai des serveurs. Ce n'est
/// pas vrai de tout le monde : une passerelle qui apparie une chaîne, un client engendré qui en tire un
/// nom de propriété, un journal que l'on filtre. L'outil signale donc la divergence et n'y touche pas —
/// uniformiser serait décider à la place de celui qui connaît la chaîne de traitement.
/// </remarks>
public sealed class HeaderCasingRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public HeaderCasingRule()
        : base("AUD-203", SpecObjectKind.Parameter, SpecObjectKind.Header)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. Named(subject)
                .GroupBy(pair => pair.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group
                    .Select(pair => pair.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => Divergent([.. group])),
        ]);
    }

    /// <summary>Les en-têtes nommés, qu'ils soient paramètres de requête ou en-têtes de réponse.</summary>
    private static IEnumerable<(string Name, SpecPointer Location)> Named(AuditSubject subject) =>
        subject.Nodes(SpecObjectKind.Parameter)
            .Where(node => string.Equals(
                Nodes.Text(node.Node, "in"), "header", StringComparison.Ordinal))
            .Select(node => (Name: Nodes.Text(node.Node, "name"), node.Location))
            .Where(pair => pair.Name is not null)
            .Select(pair => (pair.Name!, pair.Location))
            .Concat(subject.Nodes(SpecObjectKind.Header)
                .Where(node => Nodes.IsComponent(node.Location, "headers"))
                .Select(node => (Nodes.Name(node.Location), node.Location)));

    private Finding Divergent(IReadOnlyList<(string Name, SpecPointer Location)> group)
    {
        IReadOnlyList<string> spellings =
        [
            .. group.Select(member => member.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        return Report(
            "Le même en-tête est écrit de "
                + spellings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " façons : " + string.Join(", ", spellings.Select(name => "« " + name + " »")),
            group[0].Location,
            "Choisir une écriture et s'y tenir, après avoir vérifié qu'aucune passerelle ni aucun client "
                + "n'apparie l'une des autres.") with
        {
            Locations = [.. group.Select(member => member.Location)],
            Evidence = new FindingEvidence(Occurrences: group.Count, DifferingFields: spellings),
        };
    }
}
