using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Une exécution achevée, telle que les rapports la lisent.</summary>
/// <param name="RunId">L'identifiant de corrélation.</param>
/// <param name="Selection">Ce que le développeur avait choisi.</param>
/// <param name="StartedAt">Quand l'exécution a démarré.</param>
/// <param name="Duration">Combien de temps elle a pris.</param>
/// <param name="Production">La description de production, telle qu'elle a été lue.</param>
/// <param name="Candidate">La description candidate, quand le mode en comportait une.</param>
/// <param name="Diagnostics">Ce que la lecture et la validation ont trouvé à dire.</param>
/// <param name="Differences">Ce qui a changé, dans un ordre stable.</param>
/// <param name="Steps">L'état de chaque étape, pour que le rapport dise ce qui n'a pas été fait.</param>
/// <param name="Version">La version recommandée, quand le mode en comportait une.</param>
/// <param name="Audit">Ce que l'examen du fichier en service a constaté, quand le mode en comportait un.</param>
/// <param name="Plan">Ce que le nettoyage rapporterait, quand le mode le chiffrait.</param>
/// <param name="Optimization">Ce que l'application a produit, quand le développeur l'a demandée.</param>
/// <param name="Extract">L'extrait des opérations retenues, quand le mode en demandait un.</param>
/// <param name="Correction">
/// Ce qui est entré en production, et ce qui a été écarté, quand le mode écrivait une production corrigée.
/// </param>
/// <remarks>
/// Cet agrégat grandit avec les tranches : l'extraction y ajoutera son extrait, l'application sa
/// vérification et son manifeste. Il porte aujourd'hui ce qui existe, et rien de plus — un membre sans
/// producteur serait de l'inventaire.
/// </remarks>
public sealed record ApiDeltaRun(
    Guid RunId,
    RunSelection Selection,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    SpecDocument Production,
    SpecDocument? Candidate,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<Difference> Differences,
    IReadOnlyList<RunStepState> Steps,
    Versioning.VersionRecommendation? Version = null,
    Audit.AuditResult? Audit = null,
    Optimization.OptimizationPlan? Plan = null,
    Optimization.OptimizationOutcome? Optimization = null,
    Extraction.ExtractResult? Extract = null,
    Correction.CorrectionOutcome? Correction = null)
{
    /// <summary>Les différences qui cassent les appelants existants.</summary>
    public IEnumerable<Difference> Breaking =>
        Differences.Where(difference => difference.Impact == ChangeImpact.Breaking);

    /// <summary>Les différences dont l'effet n'a pas pu être établi.</summary>
    public IEnumerable<Difference> Unknown =>
        Differences.Where(difference => difference.Impact == ChangeImpact.Unknown);

    /// <summary>L'issue de l'exécution, dans le vocabulaire fixé.</summary>
    /// <remarks>
    /// Le seuil configurable et les codes qu'il commande arrivent avec la tranche qui les porte. Ce qui
    /// est établi ici est ce que l'exécution a pu faire : lire, ou non ; produire, ou non.
    /// </remarks>
    public OutcomeCode Code { get; init; } = OutcomeCode.Clean;
}
