namespace DevToolbox.Tools.ApiDelta.Core.Reporting;

/// <summary>Les noms par défaut des artefacts.</summary>
/// <remarks>
/// Fixés, parce qu'un consommateur — un relecteur, une chaîne d'intégration, une version ultérieure de cet
/// outil — les cherche par leur nom.
/// </remarks>
public static class ArtifactNames
{
    /// <summary>Le rapport lisible de la comparaison.</summary>
    public const string DeltaReport = "openapi-delta-report.md";

    /// <summary>
    /// La production consolidée : votre production, plus ce que vous avez retenu du compagnon.
    /// </summary>
    /// <remarks>
    /// Renommée le 2026-10-27, de « corrected » à « consolidated ». Un nom d'artefact est une surface
    /// extérieure : une chaîne d'intégration qui cherchait l'ancien ne le trouvera plus, et c'est pour
    /// cela que le changement est consigné dans la spécification plutôt que glissé.
    /// </remarks>
    public const string ConsolidatedDocument = "openapi-production.consolidated.yaml";

    /// <summary>Le rapport lisible par une machine.</summary>
    public const string DeltaJson = "openapi-delta.json";

    /// <summary>Le rapport d'audit.</summary>
    public const string AuditReport = "openapi-audit-report.md";

    /// <summary>Le plan d'optimisation.</summary>
    public const string OptimizationPlan = "openapi-optimization-plan.yaml";

    /// <summary>La copie optimisée.</summary>
    public const string OptimizedDocument = "openapi-production.optimized.yaml";

    /// <summary>Le manifeste des transformations appliquées.</summary>
    public const string Manifest = "openapi-optimization-manifest.yaml";

}
