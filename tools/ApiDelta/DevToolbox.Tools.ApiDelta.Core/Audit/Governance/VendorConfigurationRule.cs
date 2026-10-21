using DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Governance;

/// <summary>
/// Rapporte les configurations de passerelle qui ne se ressemblent pas d'une occurrence à l'autre.
/// </summary>
/// <remarks>
/// La même extension écrite avec des champs différents selon l'endroit est le signe d'une configuration
/// qui a dérivé : l'une des deux formes est probablement l'ancienne. L'outil le signale et n'y touche
/// jamais — il ne connaît pas la grammaire de ces extensions, et c'est exactement pourquoi il ne peut pas
/// décider laquelle des deux est la bonne.
/// </remarks>
public sealed class VendorConfigurationRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public VendorConfigurationRule()
        : base("AUD-304", SpecObjectKind.Extension)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. subject.Nodes(SpecObjectKind.Extension)
                .Where(node => Protected(subject, Nodes.Name(node.Location)))
                .Where(node => node.Node is MappingNode)
                .GroupBy(node => Nodes.Name(node.Location), StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => (group.Key, Shapes: Shapes([.. group])))
                .Where(pair => pair.Shapes.Count > 1)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => Divergent(subject, pair.Key, pair.Shapes)),
        ]);
    }

    /// <summary>Indique si l'extension relève d'un préfixe protégé.</summary>
    private static bool Protected(AuditSubject subject, string name) =>
        subject.Ruleset.ProtectedExtensionPrefixes.Any(prefix =>
            name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Les formes distinctes que prend une même extension, par jeu de clés.</summary>
    private static IReadOnlyList<IGrouping<string, AuditNode>> Shapes(
        IReadOnlyList<AuditNode> occurrences) =>
    [
        .. occurrences
            .GroupBy(
                node => string.Join(
                    ',',
                    ((MappingNode)node.Node).Entries
                        .Select(entry => entry.Key.Text)
                        .OrderBy(key => key, StringComparer.Ordinal)),
                StringComparer.Ordinal)
            .OrderBy(shape => shape.Key, StringComparer.Ordinal),
    ];

    private Finding Divergent(
        AuditSubject subject,
        string name,
        IReadOnlyList<IGrouping<string, AuditNode>> shapes)
    {
        IReadOnlyList<string> differing = FieldDifference.Between(
            shapes[0].First().Node,
            shapes[1].First().Node,
            subject.Fingerprinter,
            subject.Version);

        return Report(
            $"L'extension « {name} » est écrite sous {shapes.Count} formes différentes.",
            shapes[0].First().Location,
            "Vérifier laquelle est à jour. L'outil ne touche pas aux extensions de fournisseur.") with
        {
            Locations = [.. shapes.SelectMany(shape => shape).Select(node => node.Location)],
            Evidence = new FindingEvidence(
                Occurrences: shapes.Sum(shape => shape.Count()),
                DifferingFields: differing),
        };
    }
}
