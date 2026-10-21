using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Tout ce que le développeur a choisi, et la seule chose qui survive au processus.</summary>
/// <param name="Mode">Ce que l'exécution doit produire.</param>
/// <param name="ProductionPath">La description de production. Toujours présente.</param>
/// <param name="ProductionHash">Son empreinte au moment où elle a été lue.</param>
/// <param name="CandidatePath">La description candidate, absente pour l'audit, l'optimisation et l'extraction.</param>
/// <param name="CandidateHash">Son empreinte, quand il y en a une.</param>
/// <param name="OutputDirectory">Où les artefacts seront écrits.</param>
/// <param name="RulesetPath">Le jeu de règles, quand le développeur en a désigné un.</param>
/// <param name="SuppressionsPath">Les mises en sourdine, quand il y en a.</param>
/// <param name="ApplySafeOptimizations">Applique les transformations sûres au lieu de les proposer.</param>
/// <param name="AllowedReviewRequiredRules">
/// Les règles à revoir que le développeur a explicitement autorisées, par identifiant.
/// </param>
/// <param name="IncludeRemovals">Fait figurer les suppressions dans le correctif.</param>
/// <param name="IncludeSafeOptimizationsInPatch">Mêle le nettoyage sûr au correctif fonctionnel.</param>
/// <param name="InPlace">Réécrit la production sur place. Indisponible pour l'extraction.</param>
/// <param name="SelectedOperations">
/// Les opérations retenues, pour l'extraction seule : un chemin et une méthode chacune.
/// </param>
/// <param name="Correction">
/// Ce que le développeur a retenu pour entrer en production, pour le mode correction seul. Établi après
/// la comparaison, parce que choisir avant elle reviendrait à choisir sans voir ce qu'elle a établi.
/// </param>
/// <remarks>
/// Invariant : rien ici, même indirectement, ne peut atteindre le contenu d'un document. Il n'existe aucun
/// chemin d'ici vers un nœud, une différence ou un constat — c'est ce qui permet de projeter cette
/// sélection dans un point de reprise sans qu'une valeur puisse s'y glisser, et c'est aussi pourquoi
/// reprendre relit au lieu de restaurer.
/// </remarks>
public sealed record RunSelection(
    RunMode Mode,
    string ProductionPath,
    string ProductionHash,
    string OutputDirectory,
    string? CandidatePath = null,
    string? CandidateHash = null,
    string? RulesetPath = null,
    string? SuppressionsPath = null,
    bool ApplySafeOptimizations = false,
    IReadOnlyList<string>? AllowedReviewRequiredRules = null,
    bool IncludeRemovals = false,
    bool IncludeSafeOptimizationsInPatch = false,
    bool InPlace = false,
    IReadOnlyList<OperationSelector>? SelectedOperations = null,
    CorrectionSelection? Correction = null)
{
    /// <summary>Les règles à revoir autorisées, jamais nulles.</summary>
    public IReadOnlyList<string> AllowedReviewRequiredRules { get; init; } =
        AllowedReviewRequiredRules ?? [];

    /// <summary>Les opérations retenues, jamais nulles.</summary>
    public IReadOnlyList<OperationSelector> SelectedOperations { get; init; } = SelectedOperations ?? [];

    /// <summary>
    /// Crée une sauvegarde avant une écriture sur place, sauf refus explicite.
    /// </summary>
    /// <remarks>
    /// Vrai par défaut : la sauvegarde est le seul retour en arrière d'une écriture sur place, et il faut
    /// la refuser pour s'en passer, non la demander pour l'avoir.
    /// </remarks>
    public bool Backup { get; init; } = true;

    /// <summary>Les limites en vigueur pour cette exécution.</summary>
    public LoadLimits Limits { get; init; } = LoadLimits.Default;

    /// <summary>La politique de référence en vigueur.</summary>
    public ReferencePolicy References { get; init; } = ReferencePolicy.Closed;

    /// <summary>Indique si le mode exige une description candidate.</summary>
    public bool NeedsCandidate => Mode is RunMode.Compare or RunMode.Correct or RunMode.Everything;

    /// <summary>Indique si le mode exige une sélection d'opérations.</summary>
    public bool NeedsSelectedOperations => Mode is RunMode.Extract;

    /// <summary>Indique si le mode écrit une production corrigée.</summary>
    public bool NeedsCorrection => Mode is RunMode.Correct or RunMode.Everything;
}
