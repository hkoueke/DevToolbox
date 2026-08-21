namespace DevToolbox.Presentation.Shell;

/// <summary>Ce que le développeur a choisi de faire d'une exécution interrompue.</summary>
public enum ResumeDecision
{
    /// <summary>Ignorer l'exécution mémorisée cette fois-ci, mais la conserver pour plus tard.</summary>
    StartFresh = 0,

    /// <summary>La reprendre. Les groupes choisis sont relus, jamais restaurés de mémoire.</summary>
    Resume,

    /// <summary>Supprimer l'exécution mémorisée.</summary>
    Discard,
}
