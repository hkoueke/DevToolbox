using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les descriptions qui portent les traces de leur histoire.
/// </summary>
/// <remarks>
/// Du balisage échappé, un accent cassé par un aller-retour d'encodage, un substituant laissé en place,
/// la même phrase recopiée partout : rien de tout cela n'empêche un appel, et tout cela se retrouve
/// affiché dans un portail que des gens lisent.
/// </remarks>
public sealed class DescriptionQualityRule : AuditRule
{
    private static readonly string[] Escaped = ["&lt;", "&gt;", "&amp;", "&quot;", "&#"];

    private static readonly string[] Mojibake = ["Ã©", "Ã¨", "Ã ", "Ã§", "â€™", "Â "];

    private static readonly string[] Placeholders =
        ["TODO", "FIXME", "XXX", "A completer", "À compléter", "lorem ipsum", "bouchon"];

    private const int RepetitionThreshold = 5;

    /// <summary>Crée la règle.</summary>
    public DescriptionQualityRule()
        : base(
            "AUD-207",
            SpecObjectKind.Document,
            SpecObjectKind.Info,
            SpecObjectKind.Operation,
            SpecObjectKind.Parameter,
            SpecObjectKind.Response,
            SpecObjectKind.Schema,
            SpecObjectKind.Tag)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<ScalarNode> descriptions =
        [
            .. Visits
                .Distinct()
                .SelectMany(subject.Nodes)
                .SelectMany(node => new[]
                {
                    Nodes.Scalar(node.Node, "description"),
                    Nodes.Scalar(node.Node, "summary"),
                })
                .OfType<ScalarNode>()
                .Where(scalar => scalar.Text.Length > 0),
        ];

        return RuleReport.Evaluated(
        [
            .. Containing(descriptions, Escaped, "du balisage échappé", "Le décoder, ou le retirer."),
            .. Containing(
                descriptions,
                Mojibake,
                "un encodage suspect",
                "Relire le fichier en UTF-8 et rétablir les caractères abîmés."),
            .. Containing(
                descriptions,
                Placeholders,
                "un substituant laissé en place",
                "Écrire la description, ou retirer le champ."),
            .. Repeated(descriptions),
        ]);
    }

    private IEnumerable<Finding> Containing(
        IReadOnlyList<ScalarNode> descriptions,
        IReadOnlyList<string> needles,
        string what,
        string action) =>
        descriptions
            .Select(scalar => (scalar, Found: needles.FirstOrDefault(needle =>
                scalar.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))))
            .Where(pair => pair.Found is not null)
            .Select(pair => Report(
                $"La description contient {what} : « {pair.Found} ».",
                pair.scalar.Location,
                action));

    /// <summary>
    /// Les descriptions recopiées à l'identique un grand nombre de fois.
    /// </summary>
    /// <remarks>
    /// Un seuil, parce que deux occurrences d'une même phrase courte ne disent rien. Au-delà, la phrase ne
    /// décrit plus ce qu'elle accompagne : elle remplit un champ.
    /// </remarks>
    private IEnumerable<Finding> Repeated(IReadOnlyList<ScalarNode> descriptions) =>
        descriptions
            .GroupBy(scalar => scalar.Text, StringComparer.Ordinal)
            .Where(group => group.Count() >= RepetitionThreshold)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Report(
                $"La même description est recopiée {group.Count()} fois : « {Shorten(group.Key)} ».",
                group.First().Location,
                "Écrire ce que chaque emplacement a de particulier, ou n'en décrire aucun.") with
            {
                Locations = [.. group.Select(scalar => scalar.Location)],
                Evidence = new FindingEvidence(Occurrences: group.Count()),
            });

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "…";
}
