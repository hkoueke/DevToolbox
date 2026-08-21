namespace DevToolbox.Domain.Runs;

/// <summary>
/// Une exécution d'un outil : une suite ordonnée d'étapes nommées, chacune reprenable individuellement.
/// <see cref="RunId"/> est l'identifiant de corrélation qui relie entre elles toutes les lignes de journal
/// de l'exécution.
/// </summary>
public sealed class ToolRun
{
    private readonly List<StepState> _steps;

    /// <summary>Crée une exécution dont les étapes sont à l'état <see cref="StepStatus.Pending"/>.</summary>
    /// <param name="runId">L'identifiant de corrélation de cette exécution.</param>
    /// <param name="toolId">L'outil exécuté, par exemple <c>varcompare</c>.</param>
    /// <param name="startedAt">L'heure courante, fournie par l'horloge injectée.</param>
    /// <param name="stepNames">Les noms des étapes, dans l'ordre d'exécution.</param>
    /// <exception cref="ArgumentException">Aucune étape n'est fournie, ou un nom est vide.</exception>
    public ToolRun(Guid runId, string toolId, DateTimeOffset startedAt, IEnumerable<string> stepNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(stepNames);

        _steps = stepNames.Select(name => new StepState(name)).ToList();

        if (_steps.Count == 0)
        {
            throw new ArgumentException("A run must have at least one step.", nameof(stepNames));
        }

        RunId = runId;
        ToolId = toolId;
        StartedAt = startedAt;
    }

    /// <summary>L'identifiant de corrélation de cette exécution.</summary>
    public Guid RunId { get; }

    /// <summary>L'outil exécuté.</summary>
    public string ToolId { get; }

    /// <summary>Quand l'exécution a démarré.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Quand l'exécution s'est terminée, ou <see langword="null"/> si elle est encore en cours.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Comment l'exécution s'est terminée.</summary>
    public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;

    /// <summary>Les étapes de l'exécution, dans l'ordre.</summary>
    public IReadOnlyList<StepState> Steps => _steps;

    /// <summary>La durée de l'exécution, ou <see langword="null"/> si elle est encore en cours.</summary>
    public TimeSpan? Duration => EndedAt is null ? null : EndedAt.Value - StartedAt;

    /// <summary>
    /// Les noms des étapes terminées avec succès. C'est ce que le magasin de points de reprise conserve ;
    /// il s'agit d'une méthode et non d'une propriété car chaque appel matérialise une nouvelle collection.
    /// </summary>
    /// <returns>Les noms des étapes achevées, dans l'ordre d'exécution.</returns>
    public IReadOnlyList<string> GetCompletedStepNames() =>
        _steps.Where(step => step.IsCompleted).Select(step => step.Name).ToList();

    /// <summary>Recherche une étape par son nom.</summary>
    /// <param name="name">Le nom de l'étape.</param>
    /// <returns>L'étape, ou <see langword="null"/> si cette exécution n'en comporte pas.</returns>
    public StepState? FindStep(string name) =>
        _steps.Find(step => string.Equals(step.Name, name, StringComparison.Ordinal));

    /// <summary>Enregistre la façon dont l'exécution s'est terminée.</summary>
    /// <param name="outcome">L'issue finale.</param>
    /// <param name="at">L'heure courante, fournie par l'horloge injectée.</param>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> n'est pas une issue finale.</exception>
    public void End(RunOutcome outcome, DateTimeOffset at)
    {
        if (outcome == RunOutcome.InProgress)
        {
            throw new ArgumentException("A run cannot end as InProgress.", nameof(outcome));
        }

        Outcome = outcome;
        EndedAt = at;
    }

    /// <summary>
    /// Ramène toutes les étapes à <see cref="StepStatus.Pending"/> pour que la fonction reparte de zéro.
    /// </summary>
    public void ResetAllSteps()
    {
        foreach (StepState step in _steps)
        {
            step.Reset();
        }

        Outcome = RunOutcome.InProgress;
        EndedAt = null;
    }
}
