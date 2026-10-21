namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>L'issue d'une exécution, dans un vocabulaire fixe.</summary>
/// <remarks>
/// Fixé maintenant, et affiché à l'écran comme dans le rapport lisible par une machine, bien que cette
/// livraison ne l'expose pas comme code de sortie du processus : l'invocation automatisée est une
/// fonctionnalité à part entière, et quand elle viendra elle ajoutera une surface, pas un sens.
/// </remarks>
public enum OutcomeCode
{
    /// <summary>Rien n'atteint le seuil configuré.</summary>
    Clean,

    /// <summary>Le seuil configuré est dépassé.</summary>
    ThresholdExceeded,

    /// <summary>Un changement cassant a été détecté, et cela doit faire échouer l'exécution.</summary>
    BreakingChange,

    /// <summary>La demande elle-même est invalide : entrée manquante, sélection vide.</summary>
    InvalidUsage,

    /// <summary>Une description n'a pas pu être lue fidèlement.</summary>
    LoadFailure,

    /// <summary>Un artefact n'a pas pu être produit, ou sa vérification a échoué.</summary>
    OutputFailure,

    /// <summary>Une panne que l'outil n'avait pas prévue.</summary>
    InternalError,
}
