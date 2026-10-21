using DevToolbox.Tools.ApiDelta.Core.Configuration;

namespace DevToolbox.Tools.ApiDelta.Core.Versioning;

/// <summary>Le numéro à publier, et tout ce qui permet de le contester.</summary>
/// <param name="Baseline">La version de la production, qui sert de point de départ.</param>
/// <param name="BaselineProblem">Pourquoi elle n'a pas pu être lue, le cas échéant.</param>
/// <param name="Increment">L'incrément commandé par les changements.</param>
/// <param name="Recommended">La version recommandée, quand un point de départ existe.</param>
/// <param name="Contributors">Les changements qui ont porté la recommandation à ce niveau.</param>
/// <param name="RequiresReview">
/// Des changements n'ont pas pu être classés : la recommandation tient, mais elle ne vaut qu'autant que
/// ces changements auront été relus.
/// </param>
/// <param name="Unreviewed">Les changements que l'outil n'a pas su classer.</param>
/// <param name="ResolvedPolicy">
/// Les choix de politique tels qu'ils ont été résolus pour cette exécution. Consignés parce qu'une
/// recommandation qui dépend d'une politique doit dire laquelle était en vigueur.
/// </param>
/// <param name="Override">La version imposée par le développeur, le cas échéant.</param>
public sealed record VersionRecommendation(
    SemanticVersion? Baseline,
    string? BaselineProblem,
    VersionImpact Increment,
    SemanticVersion? Recommended,
    IReadOnlyList<VersionContributor> Contributors,
    bool RequiresReview,
    IReadOnlyList<VersionContributor> Unreviewed,
    IReadOnlyDictionary<string, VersionImpact> ResolvedPolicy,
    VersionOverride? Override = null)
{
    /// <summary>La version que portera la sortie : celle imposée, sinon celle recommandée.</summary>
    public SemanticVersion? Effective => Override?.Requested ?? Recommended;

    /// <summary>Indique si l'outil a pu recommander quelque chose.</summary>
    public bool IsUsable => Recommended is not null;
}
