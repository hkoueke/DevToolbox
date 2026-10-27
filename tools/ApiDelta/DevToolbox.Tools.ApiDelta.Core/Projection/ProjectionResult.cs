using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Ce que la projection d'un contrat de service a produit.</summary>
/// <param name="Document">Le document synthétique, au format projeté et sans étendue d'octets.</param>
/// <param name="Gaps">Ce que la projection n'a pas porté, une entrée par occurrence.</param>
/// <param name="Routes">L'itinéraire dérivé de chaque opération, et comment il l'a été.</param>
/// <param name="Figures">Les chiffres, qu'on peut recouper.</param>
/// <remarks>
/// Les écarts et les chiffres voyagent avec le document, et non à côté. Un document synthétique dont les
/// écarts se seraient perdus en route serait indiscernable d'un document fidèle — c'est la seule manière
/// dont cette fonctionnalité pourrait tromper quelqu'un, et la forme l'empêche.
/// </remarks>
public sealed record ProjectionResult(
    SpecDocument Document,
    IReadOnlyList<ProjectionGap> Gaps,
    IReadOnlyDictionary<string, DerivedRoute> Routes,
    ProjectionFigures Figures)
{
    /// <summary>
    /// Les écarts, dans un ordre que deux exécutions ne peuvent pas se partager autrement.
    /// </summary>
    /// <remarks>
    /// Rangés ici, et non par celui qui les collecte. Les attributs d'un type compilé arrivent dans
    /// l'ordre d'une table de hachage, qui n'est l'ordre de personne : un rapport dont les lignes
    /// changeraient de place d'une exécution à l'autre ferait apparaître des différences dans un
    /// contrôle d'intégration à chaque fois qu'on le relance.
    /// <para>
    /// Le tri est total : sujet, motif, construction, puis emplacement. S'arrêter plus tôt laisserait
    /// deux écarts égaux sur les premiers critères libres de permuter, ce qui est précisément le défaut
    /// qu'on écarte.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProjectionGap> Gaps { get; init; } =
    [
        .. Gaps.OrderBy(gap => gap.Subject, StringComparer.Ordinal)
            .ThenBy(gap => gap.Reason)
            .ThenBy(gap => gap.Construct, StringComparer.Ordinal)
            .ThenBy(gap => gap.Location.Document, StringComparer.Ordinal)
            .ThenBy(gap => gap.Location.Line)
            .ThenBy(gap => gap.Location.Column),
    ];

    /// <summary>Les écarts groupés par motif, avec leur nombre.</summary>
    /// <remarks>
    /// Le groupement vit ici plutôt que dans la collecte : chaque occurrence garde son emplacement, et le
    /// rapport n'affiche qu'une entrée par motif. Un développeur à qui l'on annonce « quatre cents
    /// attributs non portés » doit pouvoir aller voir le quatre-centième.
    /// </remarks>
    public IReadOnlyDictionary<ProjectionGapReason, int> ByReason =>
        Gaps.GroupBy(gap => gap.Reason)
            .ToDictionary(group => group.Key, group => group.Count());
}
