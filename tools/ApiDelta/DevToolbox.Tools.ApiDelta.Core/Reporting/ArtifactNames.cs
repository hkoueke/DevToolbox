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

    /// <summary>La production corrigée.</summary>
    public const string CorrectedDocument = "openapi-production.corrected.yaml";

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

    /// <summary>Le rapport lisible de l'extrait.</summary>
    public const string ExtractReport = "openapi-extract-report.md";

    /// <summary>L'extrait des opérations choisies.</summary>
    public const string Extract = "openapi-extract.yaml";
}
