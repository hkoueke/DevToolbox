namespace DevToolbox.Domain.Runs;

/// <summary>L'état d'avancement d'une étape nommée.</summary>
public enum StepStatus
{
    /// <summary>Pas encore démarrée.</summary>
    Pending = 0,

    /// <summary>En cours d'exécution.</summary>
    Running,

    /// <summary>Terminée avec succès.</summary>
    Completed,

    /// <summary>Terminée en échec.</summary>
    Failed,

    /// <summary>Volontairement non exécutée, par exemple un groupe écarté lors d'une reprise.</summary>
    Skipped,
}
