using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Ce que le nettoyage rapporterait, sans que rien n'ait été nettoyé.</summary>
/// <param name="Groups">Les répétitions mesurées, y compris celles qui ne valent pas d'être traitées.</param>
/// <param name="Transformations">Les transformations proposées.</param>
/// <param name="Growth">Ce que la sortie pèserait, et ce qu'on tolère qu'elle pèse.</param>
/// <remarks>
/// Le plan porte les groupes et non seulement les propositions, parce que les deux listes ne disent pas
/// la même chose : la première dit ce que le fichier a accumulé, la seconde ce que l'outil sait en
/// faire. Un groupe réel qu'aucune transformation ne couvre doit rester visible — le taire donnerait à
/// croire que l'outil a tout vu de ce qu'il sait traiter.
/// </remarks>
public sealed record OptimizationPlan(
    IReadOnlyList<RedundancyGroup> Groups,
    IReadOnlyList<Transformation> Transformations,
    GrowthCheck Growth)
{
    /// <summary>Un plan sans objet.</summary>
    public static OptimizationPlan Empty { get; } = new([], [], GrowthCheck.None);

    /// <summary>Ce que les transformations proposées feraient gagner ensemble.</summary>
    public long TotalEstimatedSaving =>
        Transformations.Sum(transformation => transformation.EstimatedBytesSaved);

    /// <summary>Le nombre de transformations effectivement appliquées.</summary>
    public int AppliedCount =>
        Transformations.Count(transformation => transformation.State == TransformationState.Applied);

    /// <summary>Les transformations que l'outil sait prouver sûres.</summary>
    public IEnumerable<Transformation> Safe =>
        Transformations.Where(transformation => transformation.Safety == SafetyClass.Safe);

    /// <summary>Les transformations qui demandent une relecture.</summary>
    public IEnumerable<Transformation> ReviewRequired =>
        Transformations.Where(transformation =>
            transformation.Safety == SafetyClass.ReviewRequired);

    /// <summary>Indique si le plan a quelque chose à proposer.</summary>
    public bool HasProposals => Transformations.Count > 0;

    /// <summary>
    /// Les répétitions réelles qu'aucune proposition ne couvre.
    /// </summary>
    /// <remarks>
    /// La question que cette liste répond est celle qu'un plan vide laisse ouverte : l'outil n'a-t-il
    /// rien trouvé, ou ne sait-il pas traiter ce qu'il a trouvé ? Deux schémas identiques sous deux noms,
    /// deux énumérations aux mêmes valeurs, un type de média recopié — autant de répétitions mesurées que
    /// cet outil signale sans savoir les défaire. Les compter ici empêche le silence de passer pour une
    /// bonne nouvelle.
    /// </remarks>
    public IEnumerable<RedundancyGroup> Uncovered =>
        Groups.Where(group => !Transformations.Any(transformation =>
            transformation.Origins.Contains(group.Location)));

    /// <summary>
    /// Indique si le plan peut mener à une publication.
    /// </summary>
    /// <remarks>
    /// Un dépassement de la croissance tolérée l'interdit, et l'interdit ici : ce que le plan annonce
    /// impossible, l'écriture n'a pas à le redécouvrir.
    /// </remarks>
    public bool Publishable => !Growth.Exceeded;
}
