using DevToolbox.Domain.Results;

namespace DevToolbox.Domain.Runs;

/// <summary>
/// L'état enregistré d'une étape nommée au sein d'une exécution. Les transitions sont contrôlées : le seul
/// chemin licite est <c>Pending -> Running -> (Completed | Failed)</c>, auquel s'ajoutent
/// <c>Pending -> Skipped</c> et un retour à <c>Running</c> depuis <c>Failed</c> lorsque le développeur
/// réessaie ou poursuit.
/// </summary>
public sealed class StepState
{
    /// <summary>Crée une étape à l'état <see cref="StepStatus.Pending"/>.</summary>
    /// <param name="name">
    /// Le nom de l'étape, par exemple <c>IdentifyProject</c> ou <c>RetrieveGroup:42</c>. Ce nom apparaît
    /// dans les journaux et dans l'invite de reprise : il doit parler à un développeur.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="name"/> est vide ou ne contient que des espaces.</exception>
    public StepState(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A step must have a name.", nameof(name));
        }

        Name = name;
    }

    /// <summary>Le nom de l'étape.</summary>
    public string Name { get; }

    /// <summary>L'état courant de l'étape.</summary>
    public StepStatus Status { get; private set; } = StepStatus.Pending;

    /// <summary>Quand l'étape a démarré pour la dernière fois.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Quand l'étape s'est arrêtée pour la dernière fois.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// Pourquoi l'étape a échoué, sous forme de message affichable. Jamais une trace d'exception.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>Indique si cette étape s'est déjà terminée avec succès.</summary>
    public bool IsCompleted => Status == StepStatus.Completed;

    /// <summary>Marque l'étape comme en cours d'exécution.</summary>
    /// <param name="at">L'heure courante, fournie par l'horloge injectée.</param>
    /// <exception cref="InvalidOperationException">L'étape est déjà en cours ou déjà terminée.</exception>
    public void Start(DateTimeOffset at)
    {
        if (Status is StepStatus.Running or StepStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Step '{Name}' cannot start from status {Status}.");
        }

        Status = StepStatus.Running;
        StartedAt = at;
        EndedAt = null;
        FailureReason = null;
    }

    /// <summary>Marque l'étape comme terminée avec succès.</summary>
    /// <param name="at">L'heure courante, fournie par l'horloge injectée.</param>
    /// <exception cref="InvalidOperationException">L'étape n'est pas en cours d'exécution.</exception>
    public void Complete(DateTimeOffset at)
    {
        if (Status != StepStatus.Running)
        {
            throw new InvalidOperationException(
                $"Step '{Name}' cannot complete from status {Status}.");
        }

        Status = StepStatus.Completed;
        EndedAt = at;
    }

    /// <summary>Marque l'étape comme échouée.</summary>
    /// <param name="at">L'heure courante, fournie par l'horloge injectée.</param>
    /// <param name="failure">L'échec, dont le message doit déjà être affichable tel quel.</param>
    /// <exception cref="InvalidOperationException">L'étape n'est pas en cours d'exécution.</exception>
    public void Fail(DateTimeOffset at, Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (Status != StepStatus.Running)
        {
            throw new InvalidOperationException(
                $"Step '{Name}' cannot fail from status {Status}.");
        }

        Status = StepStatus.Failed;
        EndedAt = at;
        FailureReason = failure.Message;
    }

    /// <summary>Marque l'étape comme volontairement ignorée.</summary>
    /// <param name="at">L'heure courante, fournie par l'horloge injectée.</param>
    public void Skip(DateTimeOffset at)
    {
        Status = StepStatus.Skipped;
        EndedAt = at;
    }

    /// <summary>
    /// Ramène l'étape à <see cref="StepStatus.Pending"/> en effaçant ses horodatages. Utilisé lorsque le
    /// développeur relance toute la fonction depuis le début.
    /// </summary>
    public void Reset()
    {
        Status = StepStatus.Pending;
        StartedAt = null;
        EndedAt = null;
        FailureReason = null;
    }
}
