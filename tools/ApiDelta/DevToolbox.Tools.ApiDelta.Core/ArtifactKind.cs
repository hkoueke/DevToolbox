namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>La sorte d'un artefact produit par une exécution.</summary>
public enum ArtifactKind
{
    /// <summary>Le rapport lisible de la comparaison.</summary>
    DeltaReport,

    /// <summary>La production consolidée : la production, plus ce que le développeur a retenu.</summary>
    ConsolidatedDocument,

    /// <summary>Le rapport lisible par une machine.</summary>
    DeltaJson,

    /// <summary>Le rapport d'audit.</summary>
    AuditReport,

    /// <summary>Le plan d'optimisation, qui ne change rien.</summary>
    OptimizationPlan,

    /// <summary>La copie optimisée, vérifiée équivalente avant publication.</summary>
    OptimizedDocument,

    /// <summary>Le manifeste des transformations appliquées.</summary>
    Manifest,

}
