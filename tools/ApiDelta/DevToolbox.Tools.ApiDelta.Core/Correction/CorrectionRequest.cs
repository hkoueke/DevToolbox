using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Versioning;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce qu'il faut pour écrire la production corrigée.</summary>
/// <param name="Production">La description en service, dont les octets seront recopiés.</param>
/// <param name="Candidate">La description candidate, d'où vient le contenu retenu.</param>
/// <param name="Differences">Ce que la comparaison a trouvé.</param>
/// <param name="Selection">Ce que le développeur a retenu.</param>
/// <param name="Closure">Les composants dont le contenu retenu a besoin.</param>
/// <param name="Version">La version à inscrire, quand elle a pu être établie.</param>
/// <param name="Formatting">La mise en forme des parties neuves seulement.</param>
/// <param name="IncludeRemovals">Autorise les suppressions, qui ne sont jamais faites sans demande.</param>
public sealed record CorrectionRequest(
    SpecDocument Production,
    SpecDocument Candidate,
    IReadOnlyList<Difference> Differences,
    CorrectionSelection Selection,
    DependencyClosure Closure,
    VersionRecommendation? Version,
    CanonicalFormatting Formatting,
    bool IncludeRemovals = false);
