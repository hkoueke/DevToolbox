namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Conserve et relit les exécutions interrompues afin de pouvoir les proposer à la reprise.
/// </summary>
public interface IRunCheckpointStore
{
    /// <summary>Écrit ou remplace le point de reprise d'une exécution.</summary>
    /// <param name="run">L'état à conserver. Il ne doit porter aucune valeur de variable.</param>
    /// <param name="cancellationToken">Annule l'écriture.</param>
    /// <returns>Une tâche qui s'achève quand le point de reprise est durable.</returns>
    Task SaveAsync(PersistedRun run, CancellationToken cancellationToken);

    /// <summary>Liste les exécutions interrompues encore reprenables pour un outil.</summary>
    /// <param name="toolId">L'outil dont il faut lister les exécutions.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Les exécutions reprenables, la plus récente en tête.</returns>
    Task<IReadOnlyList<PersistedRun>> ListResumableAsync(string toolId, CancellationToken cancellationToken);

    /// <summary>Oublie une exécution mémorisée.</summary>
    /// <param name="runId">L'exécution à oublier.</param>
    /// <param name="cancellationToken">Annule la suppression.</param>
    /// <returns>Une tâche qui s'achève quand le point de reprise a disparu.</returns>
    Task DiscardAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Supprime les points de reprise plus vieux que la fenêtre de conservation.</summary>
    /// <param name="maxAge">La fenêtre de conservation, sept jours par défaut.</param>
    /// <param name="cancellationToken">Annule le nettoyage.</param>
    /// <returns>Le nombre de points de reprise supprimés.</returns>
    Task<int> SweepExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken);
}
