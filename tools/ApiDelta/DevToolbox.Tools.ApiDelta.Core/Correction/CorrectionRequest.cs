using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Versioning;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce qu'il faut pour écrire la production consolidée.</summary>
/// <param name="Production">La description en service, dont les octets seront recopiés.</param>
/// <param name="Candidate">La description candidate, d'où vient le contenu retenu.</param>
/// <param name="Differences">Ce que la comparaison a trouvé.</param>
/// <param name="Selection">Ce que le développeur a retenu.</param>
/// <param name="Closure">Les composants dont le contenu retenu a besoin.</param>
/// <param name="Version">La version à inscrire, quand elle a pu être établie.</param>
/// <param name="Formatting">La mise en forme des parties neuves seulement.</param>
/// <param name="IncludeRemovals">Autorise les suppressions, qui ne sont jamais faites sans demande.</param>
/// <param name="Authoring">
/// Les routes que le développeur a composées : leur chemin, leur verbe et où elles se placent. Nul quand
/// le compagnon n'est pas un contrat de service, auquel cas une route reprise garde la sienne.
/// </param>
/// <param name="Footing">
/// Ce sur quoi la sélection repose, quand le candidat est projeté depuis un contrat de service.
/// </param>
/// <remarks>
/// La base est facultative, et son absence est ce qui distingue les deux chemins : un candidat OpenAPI
/// donne ses propres octets, un contrat de service ne donne qu'une traduction. Aucun drapeau ne le dit,
/// et c'est voulu — un drapeau pourrait être posé à tort, tandis qu'une base absente ne peut pas mentir.
/// </remarks>
public sealed record CorrectionRequest(
    SpecDocument Production,
    SpecDocument Candidate,
    IReadOnlyList<Difference> Differences,
    CorrectionSelection Selection,
    DependencyClosure Closure,
    VersionRecommendation? Version,
    CanonicalFormatting Formatting,
    bool IncludeRemovals = false,
    WsdlFooting? Footing = null,
    Authoring.AuthoringRecord? Authoring = null);
