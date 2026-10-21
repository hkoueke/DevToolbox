using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Versioning;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce que l'outil est sur le point d'insérer, dit avant qu'un octet ne soit écrit.</summary>
/// <param name="Routes">Les routes qui entreront entières.</param>
/// <param name="Differences">Les différences qui entreront une par une, avec ce qu'elles valent.</param>
/// <param name="Components">Le nombre de composants que la fermeture emportera.</param>
/// <param name="Version">La version qui sera inscrite, quand elle a pu être établie.</param>
/// <param name="Declined">Ce qui a été retenu et ne peut pas être porté, avec la raison.</param>
/// <remarks>
/// Un plan est un énoncé, pas une action : le construire ne modifie rien. Il existe pour que le
/// développeur voie la liste complète et l'accepte, et pour que la vérification d'après ait de quoi se
/// mesurer — sans lui, « ce qui a changé ne diffère que comme demandé » n'aurait rien à quoi se comparer.
/// </remarks>
public sealed record CorrectionPlan(
    IReadOnlyList<OperationSelector> Routes,
    IReadOnlyList<PlannedDifference> Differences,
    int Components,
    VersionRecommendation? Version,
    IReadOnlyList<DeclinedCorrection>? Declined = null)
{
    /// <summary>Ce qui a été écarté, jamais nul.</summary>
    public IReadOnlyList<DeclinedCorrection> Declined { get; init; } = Declined ?? [];

    /// <summary>Le nombre d'éléments que le plan porte.</summary>
    public int Count => Routes.Count + Differences.Count;

    /// <summary>Indique que le plan n'insérerait rien.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Indique qu'au moins un élément retenu casse les appelants existants.</summary>
    /// <remarks>
    /// Annoncé, parce qu'un développeur a le droit de vouloir une rupture — mais pas celui de l'emporter
    /// sans l'avoir su.
    /// </remarks>
    public bool CarriesBreaking =>
        Differences.Any(difference => difference.Impact is ChangeImpact.Breaking);
}
