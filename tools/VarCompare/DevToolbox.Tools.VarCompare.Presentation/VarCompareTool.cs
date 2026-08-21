using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Steps;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation;

/// <summary>
/// L'outil varcompare : comparer les variables d'au moins deux groupes Azure DevOps, en lecture seule.
/// </summary>
public sealed class VarCompareTool : ITool
{
    private readonly IStepRunner _runner;
    private readonly IReadOnlyList<IToolStep> _steps;
    private readonly ComparisonSession _session;
    private readonly ITargetAnnouncer _announcer;
    private readonly ServerTarget _target;
    private readonly IClock _clock;
    private readonly ResumeCoordinator _resume;
    private readonly AbortSummaryRenderer _abortSummary;
    private readonly IAnsiConsole _console;

    /// <summary>Crée l'outil.</summary>
    /// <param name="runner">Déroule les étapes avec points de reprise et reprise sur erreur.</param>
    /// <param name="steps">Les cinq étapes, dans l'ordre.</param>
    /// <param name="session">Affiche la comparaison et gère pagination, filtrage et détail.</param>
    /// <param name="announcer">Annonce la cible avant toute lecture.</param>
    /// <param name="target">La cible de la boîte à outils.</param>
    /// <param name="clock">Fournit l'heure de démarrage de l'exécution.</param>
    /// <param name="resume">Propose une exécution interrompue et restaure sa sélection.</param>
    /// <param name="abortSummary">Indique jusqu'où une exécution est allée quand elle s'arrête avant terme.</param>
    /// <param name="console">La console où écrire.</param>
    public VarCompareTool(
        IStepRunner runner,
        IEnumerable<IToolStep> steps,
        ComparisonSession session,
        ITargetAnnouncer announcer,
        ServerTarget target,
        IClock clock,
        ResumeCoordinator resume,
        AbortSummaryRenderer abortSummary,
        IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(abortSummary);
        ArgumentNullException.ThrowIfNull(console);

        _runner = runner;
        _steps = [.. steps];
        _session = session;
        _announcer = announcer;
        _target = target;
        _clock = clock;
        _resume = resume;
        _abortSummary = abortSummary;
        _console = console;
    }

    /// <inheritdoc />
    public string Id => "varcompare";

    /// <inheritdoc />
    public string DisplayName => "varcompare";

    /// <inheritdoc />
    public string Tab => "Azure DevOps";

    /// <inheritdoc />
    public string Description => "comparer des groupes de variables Azure DevOps (lecture seule)";

    /// <inheritdoc />
    public async Task<ToolRun> RunAsync(CancellationToken cancellationToken)
    {
        // Dire quel serveur et quelle collection vont être lus, avant de lire quoi que ce soit.
        _announcer.AnnounceTarget(new TargetAnnouncement(_target.Host, _target.Collection));

        ToolRun run = new(
            Guid.NewGuid(),
            Id,
            _clock.UtcNow,
            [.. _steps.Select(step => step.Name)]);

        ToolRunContext context = new(run);

        // Une exécution interrompue est proposée avant toute lecture. Seule la sélection revient, si bien que
        // les groupes sont relus au lieu d'être restaurés de mémoire.
        await _resume.TryResumeAsync(Id, context, cancellationToken).ConfigureAwait(false);

        await _runner.RunAsync(context, _steps, cancellationToken).ConfigureAwait(false);

        if (run.Outcome == RunOutcome.Success)
        {
            await ShowUntilDoneAsync(run, context, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            RenderUnfinished(run);
            _abortSummary.Render(run, context);

            _console.MarkupLine("[grey]Appuyez sur Entrée pour revenir au menu.[/]");
            _console.Input.ReadKey(intercept: true);
        }

        return run;
    }

    /// <summary>
    /// Affiche la comparaison, en relisant les groupes chaque fois que le développeur demande un
    /// rafraîchissement.
    /// </summary>
    /// <remarks>
    /// Une comparaison reflète l'instant où ses groupes ont été lus. Le rafraîchissement existe pour qu'une
    /// modification faite par quelqu'un d'autre en cours de session soit rattrapable sans quitter l'outil. Il
    /// jette les clichés conservés afin que l'étape de lecture relise réellement.
    /// </remarks>
    private async Task ShowUntilDoneAsync(
        ToolRun run,
        ToolRunContext context,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            VariableComparison? comparison =
                context.Get<VariableComparison>(VarCompareContextKeys.Comparison);

            Dictionary<int, VariableGroupSnapshot>? snapshots =
                context.Get<Dictionary<int, VariableGroupSnapshot>>(VarCompareContextKeys.Snapshots);

            if (comparison is null || snapshots is null)
            {
                return;
            }

            IReadOnlyList<VariableGroupSnapshot> ordered =
            [
                .. comparison.Groups
                    .Select(group => snapshots.GetValueOrDefault(group.Id))
                    .Where(snapshot => snapshot is not null)
                    .Select(snapshot => snapshot!),
            ];

            bool refreshRequested = _session.Show(comparison, ordered, cancellationToken);

            if (!refreshRequested)
            {
                return;
            }

            context.Set(VarCompareContextKeys.Snapshots, new Dictionary<int, VariableGroupSnapshot>());
            RestartRetrievalSteps(run);

            await _runner.RunAsync(context, _steps, cancellationToken).ConfigureAwait(false);

            if (run.Outcome != RunOutcome.Success)
            {
                RenderUnfinished(run);
                return;
            }
        }
    }

    /// <summary>Remet en attente les étapes de lecture et de comparaison pour qu'un rafraîchissement les rejoue.</summary>
    private static void RestartRetrievalSteps(ToolRun run)
    {
        foreach (string name in new[] { RetrieveGroupsStep.StepName, BuildComparisonStep.StepName })
        {
            run.FindStep(name)?.Reset();
        }
    }

    private void RenderUnfinished(ToolRun run)
    {
        StepState? failed = run.Steps.FirstOrDefault(step => step.Status == StepStatus.Failed);

        if (failed?.FailureReason is { } reason)
        {
            _console.MarkupLine(
                $"[red]{Markup.Escape(failed.Name)} ne s'est pas achevée :[/] {Markup.Escape(reason)}");
        }
        else
        {
            _console.MarkupLine("[yellow]L'exécution ne s'est pas achevée.[/]");
        }
    }

}
