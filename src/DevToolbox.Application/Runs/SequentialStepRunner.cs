using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Internal;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Application.Runs;

/// <summary>
/// Déroule les étapes d'un outil dans l'ordre déclaré, en enregistrant un point de reprise après chaque
/// succès.
/// </summary>
/// <remarks>
/// En cas d'échec, quatre possibilités sont proposées au développeur : relancer l'étape, poursuivre au-delà
/// en conservant ce qu'elle a produit, reprendre toute la fonction depuis le début, ou abandonner.
/// </remarks>
public sealed class SequentialStepRunner : IStepRunner
{
    private readonly IClock _clock;
    private readonly IRunCheckpointStore _checkpointStore;
    private readonly IRunCheckpointFactory _checkpointFactory;
    private readonly IRecoveryPrompt _recoveryPrompt;
    private readonly ILogger<SequentialStepRunner> _logger;

    /// <summary>Crée un moteur d'étapes.</summary>
    /// <param name="clock">Fournit l'heure courante.</param>
    /// <param name="checkpointStore">Conserve l'avancement après chaque étape réussie.</param>
    /// <param name="checkpointFactory">Construit le point de reprise de l'outil en cours.</param>
    /// <param name="recoveryPrompt">Demande au développeur quoi faire d'une étape en échec.</param>
    /// <param name="logger">Reçoit les transitions d'étape.</param>
    public SequentialStepRunner(
        IClock clock,
        IRunCheckpointStore checkpointStore,
        IRunCheckpointFactory checkpointFactory,
        IRecoveryPrompt recoveryPrompt,
        ILogger<SequentialStepRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(checkpointStore);
        ArgumentNullException.ThrowIfNull(checkpointFactory);
        ArgumentNullException.ThrowIfNull(recoveryPrompt);
        ArgumentNullException.ThrowIfNull(logger);

        _clock = clock;
        _checkpointStore = checkpointStore;
        _checkpointFactory = checkpointFactory;
        _recoveryPrompt = recoveryPrompt;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ToolRun> RunAsync(
        ToolRunContext context,
        IReadOnlyList<IToolStep> steps,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);

        ToolRun run = context.Run;
        int cursor = 0;

        // Une boucle « while » plutôt qu'un « for » : la reprise recule le curseur (relance de l'étape) ou
        // le ramène au début (reprise complète). C'est donc un petit automate, pas un simple parcours.
        while (cursor < steps.Count)
        {
            IToolStep step = steps[cursor];

            if (cancellationToken.IsCancellationRequested)
            {
                run.End(RunOutcome.Cancelled, _clock.UtcNow);
                return run;
            }

            StepState state = FindStepOrThrow(run, step.Name);

            if (state.IsCompleted || state.Status == StepStatus.Skipped)
            {
                // Déjà faite, ou volontairement passée : poursuivre ne doit pas refaire le travail acquis.
                cursor++;
                continue;
            }

            Result outcome = await ExecuteStepAsync(context, step, state, cancellationToken)
                .ConfigureAwait(false);

            if (outcome.IsSuccess)
            {
                await SaveCheckpointAsync(context, cancellationToken).ConfigureAwait(false);
                cursor++;
                continue;
            }

            if (outcome.Failure!.Reason == FailureReason.Cancelled)
            {
                run.End(RunOutcome.Cancelled, _clock.UtcNow);
                return run;
            }

            RecoveryOutcome recovery = await RecoverAsync(context, state, cancellationToken)
                .ConfigureAwait(false);

            if (recovery.Abandoned)
            {
                run.End(RunOutcome.Abandoned, _clock.UtcNow);
                return run;
            }

            cursor = recovery.NextCursor(cursor);
        }

        run.End(RunOutcome.Success, _clock.UtcNow);
        return run;
    }

    private static StepState FindStepOrThrow(ToolRun run, string name) =>
        run.FindStep(name)
        ?? throw new InvalidOperationException(
            $"Step '{name}' is not declared on run {run.RunId}.");

    /// <summary>Demande quoi faire d'une étape en échec, puis applique la réponse.</summary>
    private async Task<RecoveryOutcome> RecoverAsync(
        ToolRunContext context,
        StepState state,
        CancellationToken cancellationToken)
    {
        RecoveryChoice choice = await _recoveryPrompt
            .AskAsync(state, cancellationToken)
            .ConfigureAwait(false);

        string chosen = choice.ToString();
        Log.RecoveryChosen(_logger, state.Name, chosen);

        switch (choice)
        {
            case RecoveryChoice.RetryStep:
                // Rejouer la même étape ; rien d'autre ne bouge.
                return new RecoveryOutcome(Abandoned: false, RestartFromBeginning: false, Retry: true);

            case RecoveryChoice.ContinueFromStep:
                // Poursuivre au-delà de l'étape en échec en gardant ce qu'elle a tout de même produit. Pour
                // cet outil, un jeu de groupes partiellement lu parvient donc quand même à la comparaison,
                // qui décide elle-même s'il en reste assez pour valoir la peine d'être affichée.
                state.Skip(_clock.UtcNow);
                await SaveCheckpointAsync(context, cancellationToken).ConfigureAwait(false);
                return new RecoveryOutcome(Abandoned: false, RestartFromBeginning: false, Retry: false);

            case RecoveryChoice.RestartAll:
                // Tout ce qui était conservé est jeté, pour que l'exécution reparte vraiment de zéro plutôt
                // que de se poursuivre sur un état périmé.
                context.Run.ResetAllSteps();
                context.Clear();
                return new RecoveryOutcome(Abandoned: false, RestartFromBeginning: true, Retry: false);

            default:
                return new RecoveryOutcome(Abandoned: true, RestartFromBeginning: false, Retry: false);
        }
    }

    /// <summary>Ce que le choix de reprise du développeur implique pour le curseur du moteur.</summary>
    private sealed record RecoveryOutcome(bool Abandoned, bool RestartFromBeginning, bool Retry)
    {
        /// <summary>Où l'exécution reprend une fois le choix appliqué.</summary>
        /// <param name="current">La position du curseur sur l'étape en échec.</param>
        /// <returns>La position suivante du curseur.</returns>
        internal int NextCursor(int current)
        {
            if (RestartFromBeginning)
            {
                return 0;
            }

            return Retry ? current : current + 1;
        }
    }

    private async Task<Result> ExecuteStepAsync(
        ToolRunContext context,
        IToolStep step,
        StepState state,
        CancellationToken cancellationToken)
    {
        state.Start(_clock.UtcNow);
        Log.StepStarted(_logger, step.Name);

        Result outcome;

        try
        {
            outcome = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            outcome = Result.Fail(FailureReason.Cancelled, "The run was cancelled.");
        }

        if (outcome.IsSuccess)
        {
            state.Complete(_clock.UtcNow);

            // Calculé dans une variable locale plutôt que passé sous forme d'appel : l'analyseur proscrit
            // les invocations de méthode en argument de journalisation, et ce calcul est assez peu coûteux
            // pour être fait systématiquement.
            long elapsedMilliseconds = ElapsedMilliseconds(state);
            Log.StepCompleted(_logger, step.Name, elapsedMilliseconds);
        }
        else
        {
            state.Fail(_clock.UtcNow, outcome.Failure!);
            Log.StepFailed(_logger, step.Name, outcome.Failure!.Reason.ToString());
        }

        return outcome;
    }

    /// <summary>La durée d'une étape, pour la ligne de journal de fin.</summary>
    private static long ElapsedMilliseconds(StepState state) =>
        state.StartedAt is null || state.EndedAt is null
            ? 0L
            : (long)(state.EndedAt.Value - state.StartedAt.Value).TotalMilliseconds;

    private async Task SaveCheckpointAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        PersistedRun? checkpoint = _checkpointFactory.TryCreate(context);

        if (checkpoint is not null)
        {
            await _checkpointStore.SaveAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        }
    }
}
