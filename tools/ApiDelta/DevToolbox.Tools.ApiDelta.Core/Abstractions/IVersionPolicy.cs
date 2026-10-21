using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Versioning;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Recommande la version à publier.</summary>
public interface IVersionPolicy
{
    /// <summary>Recommande un numéro à partir de la production et de ce qui a changé.</summary>
    /// <param name="differences">Les différences détectées.</param>
    /// <param name="production">La description de production, qui porte le point de départ.</param>
    /// <param name="policy">Les choix de l'équipe là où la spécification laisse le choix.</param>
    /// <param name="requested">Une version imposée par le développeur, le cas échéant.</param>
    /// <param name="forced">Le développeur a confirmé vouloir passer outre la recommandation.</param>
    /// <returns>La recommandation, et de quoi la contester.</returns>
    VersionRecommendation Recommend(
        IReadOnlyList<Difference> differences,
        SpecDocument production,
        VersioningPolicy policy,
        SemanticVersion? requested = null,
        bool forced = false);
}
