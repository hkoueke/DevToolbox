using DevToolbox.Application.Abstractions;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// L'observateur employé quand rien n'écoute. Enregistré par défaut pour que le pipeline se construise
/// identiquement là où il n'y a pas de console : un test, ou un futur mode non interactif.
/// </summary>
public sealed class NullRetryObserver : IRetryObserver
{
    /// <inheritdoc />
    public void Retrying(int attempt, int maxAttempts, TimeSpan delay)
    {
        // Volontairement sans effet.
    }

    /// <inheritdoc />
    public void Throttled(TimeSpan retryAfter)
    {
        // Volontairement sans effet.
    }
}
