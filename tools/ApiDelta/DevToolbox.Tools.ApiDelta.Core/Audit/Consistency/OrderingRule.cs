using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les associations dont les clés ne suivent aucun ordre stable.
/// </summary>
/// <remarks>
/// L'ordre d'une association ne veut rien dire pour OpenAPI, et c'est précisément ce qui le rend nuisible :
/// deux outils qui réécrivent le fichier le rangent chacun à sa façon, et le diff devient illisible. Un
/// ordre convenu rend les différences lisibles sans rien changer au contrat.
/// <para>
/// Seules les associations sont concernées. Une suite dont l'ordre a un sens — les alternatives de
/// sécurité, une composition — n'est jamais réordonnée, et cette règle ne la regarde pas.
/// </para>
/// </remarks>
public sealed class OrderingRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public OrderingRule()
        : base(
            "AUD-201",
            SpecObjectKind.Paths,
            SpecObjectKind.PathItem,
            SpecObjectKind.Responses,
            SpecObjectKind.Components)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. Unsorted(subject, SpecObjectKind.Paths),
            .. Unsorted(subject, SpecObjectKind.Responses),
            .. Unsorted(subject, SpecObjectKind.Components),
            .. Methods(subject),
        ]);
    }

    /// <summary>
    /// Les associations dont les clés ne sont pas triées.
    /// </summary>
    /// <remarks>
    /// Le message ne nomme pas ce que les clés sont, parce que la même sorte d'association sert à deux
    /// choses : sous <c>components</c>, les clés de <c>responses</c> sont des noms de composants, non des
    /// codes de statut. Dire « les codes de réponse » là serait faux, et l'emplacement affiché suffit.
    /// </remarks>
    private IEnumerable<Finding> Unsorted(AuditSubject subject, SpecObjectKind kind) =>
        subject.Nodes(kind)
            .Select(node => node.Mapping)
            .OfType<MappingNode>()
            .Where(mapping => mapping.Entries.Count > 1)
            .Where(mapping => !Sorted([.. mapping.Entries.Select(entry => entry.Key.Text)]))
            .Select(mapping => Report(
                "Les clés de cette association ne sont pas rangées dans un ordre stable.",
                mapping.Location,
                "Les ranger une fois pour toutes : l'ordre ne change rien au contrat et rend les "
                    + "différences lisibles."));

    /// <summary>
    /// Les éléments de chemin dont les méthodes ne suivent pas l'ordre convenu.
    /// </summary>
    /// <remarks>
    /// Pour les méthodes, l'ordre convenu n'est pas l'ordre alphabétique mais celui du cycle de vie —
    /// lire, créer, remplacer, modifier, supprimer — parce que c'est celui dans lequel on les lit.
    /// </remarks>
    private IEnumerable<Finding> Methods(AuditSubject subject) =>
        subject.Nodes(SpecObjectKind.PathItem)
            .Select(node => node.Mapping)
            .OfType<MappingNode>()
            .Where(item => !Ordered([.. item.Entries.Select(entry => entry.Key.Text)]))
            .Select(item => Report(
                "Les méthodes de ce chemin ne suivent pas l'ordre convenu : "
                    + string.Join(", ", MethodOrder),
                item.Location,
                "Les ranger dans l'ordre du cycle de vie."));

    private static IReadOnlyList<string> MethodOrder { get; } =
        ["get", "post", "put", "patch", "delete", "head", "options", "trace"];

    private static bool Sorted(IReadOnlyList<string> keys) =>
        keys
            .Zip(keys.Skip(1), (left, right) => string.CompareOrdinal(left, right) <= 0)
            .All(ordered => ordered);

    private static bool Ordered(IReadOnlyList<string> keys)
    {
        IReadOnlyList<int> ranks =
        [
            .. keys
                .Select(key => MethodOrder.ToList().IndexOf(key))
                .Where(rank => rank >= 0),
        ];

        return ranks.Zip(ranks.Skip(1), (left, right) => left <= right).All(ordered => ordered);
    }
}
