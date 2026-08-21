namespace DevToolbox.Domain.Runs;

/// <summary>Comment une exécution s'est terminée.</summary>
public enum RunOutcome
{
    /// <summary>En cours.</summary>
    InProgress = 0,

    /// <summary>Toutes les étapes ont abouti.</summary>
    Success,

    /// <summary>Une étape a échoué et le développeur ne s'en est pas remis.</summary>
    Failed,

    /// <summary>Le développeur a annulé, par exemple avec Ctrl+C.</summary>
    Cancelled,

    /// <summary>Le développeur a choisi d'abandonner au moment de la reprise sur erreur.</summary>
    Abandoned,
}
