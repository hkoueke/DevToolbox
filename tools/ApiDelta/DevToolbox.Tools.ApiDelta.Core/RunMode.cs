namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Ce qu'une exécution d'apidelta est censée produire.</summary>
public enum RunMode
{
    /// <summary>Comparer le candidat à la production.</summary>
    Compare,

    /// <summary>Comparer, puis écrire la production corrigée de ce que le développeur retient.</summary>
    Correct,

    /// <summary>Auditer la production seule.</summary>
    Audit,

    /// <summary>Analyser ce que le nettoyage de la production rapporterait.</summary>
    Optimize,

    /// <summary>Extraire les opérations choisies dans une description autonome.</summary>
    Extract,

    /// <summary>Tout, sauf extraire : une extraction suppose une sélection, et nulle sélection ne se devine.</summary>
    Everything,
}
