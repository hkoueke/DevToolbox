using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Core.Versioning;
using DevToolbox.Tools.ApiDelta.Presentation.Rendering;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation;

/// <summary>
/// L'outil apidelta : comparer une description OpenAPI candidate à celle de production, et auditer ce que
/// la production a accumulé.
/// </summary>
/// <remarks>
/// Le premier outil de la boîte qui ne s'adresse à personne : deux fichiers locaux, aucune requête, aucun
/// identifiant. C'est pourquoi il déclare <see cref="RequiresServer"/> à <see langword="false"/>, et c'est
/// ce qui permet de l'utiliser sur un poste qui n'a jamais vu de serveur Azure DevOps.
/// </remarks>
public sealed class ApiDeltaTool : ITool
{
    private readonly IStepRunner _runner;
    private readonly IReadOnlyList<IToolStep> _steps;
    private readonly ApiDeltaResumeCoordinator _resume;
    private readonly SelectionSummaryRenderer _summary;
    private readonly DiagnosticsView _diagnostics;
    private readonly ComparisonView _comparison;
    private readonly FindingsView _findings;
    private readonly ProposalsView _proposals;
    private readonly IClock _clock;
    private readonly IAnsiConsole _console;

    /// <summary>Crée l'outil.</summary>
    /// <param name="runner">Déroule les étapes avec points de reprise et reprise sur erreur.</param>
    /// <param name="steps">Les étapes, dans l'ordre.</param>
    /// <param name="resume">Propose une exécution interrompue et vérifie qu'elle vaut encore.</param>
    /// <param name="summary">Affiche ce qui a été retenu et l'état des étapes.</param>
    /// <param name="diagnostics">Affiche ce que la lecture et la validation ont trouvé à dire.</param>
    /// <param name="comparison">Affiche le compte des différences, puis le détail demandé.</param>
    /// <param name="findings">Affiche ce que l'examen du fichier de production a constaté.</param>
    /// <param name="proposals">Affiche ce que le nettoyage rapporterait.</param>
    /// <param name="clock">Fournit l'heure de démarrage de l'exécution.</param>
    /// <param name="console">La console où écrire.</param>
    public ApiDeltaTool(
        IStepRunner runner,
        IEnumerable<IToolStep> steps,
        ApiDeltaResumeCoordinator resume,
        SelectionSummaryRenderer summary,
        DiagnosticsView diagnostics,
        ComparisonView comparison,
        FindingsView findings,
        ProposalsView proposals,
        IClock clock,
        IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(console);

        _runner = runner;
        _steps = [.. steps];
        _resume = resume;
        _summary = summary;
        _diagnostics = diagnostics;
        _comparison = comparison;
        _findings = findings;
        _proposals = proposals;
        _clock = clock;
        _console = console;
    }

    /// <inheritdoc />
    public string Id => "apidelta";

    /// <inheritdoc />
    public string DisplayName => "apidelta";

    /// <inheritdoc />
    public string Tab => "OpenAPI";

    /// <inheritdoc />
    public string Description =>
        "comparer, vérifier et nettoyer des fichiers OpenAPI (Swagger)";

    /// <inheritdoc />
    /// <remarks>
    /// Cet outil lit deux fichiers locaux et n'émet aucune requête : exiger une adresse de serveur pour
    /// atteindre son onglet reviendrait à exiger d'en inventer une.
    /// </remarks>
    public bool RequiresServer => false;

    /// <summary>
    /// Les étapes que cette instance exécutera, dans l'ordre, par leur nom.
    /// </summary>
    /// <remarks>
    /// Exposées pour qu'un essai puisse confronter la chaîne <em>réellement construite</em> à l'ordre
    /// que <see cref="ApiDeltaStepNames.InOrder"/> déclare. Rien ne les confrontait, et deux étapes
    /// écrites, enregistrées et éprouvées une par une sont restées hors de la chaîne pendant deux
    /// tranches : l'outil ne savait pas lire un contrat de service alors que tout ce qu'il faut pour le
    /// lire existait et passait ses essais.
    /// </remarks>
    public IReadOnlyList<string> StepNames => [.. _steps.Select(step => step.Name)];

    /// <inheritdoc />
    public async Task<ToolRun> RunAsync(CancellationToken cancellationToken)
    {
        ToolRun run = new(Guid.NewGuid(), Id, _clock.UtcNow, [.. StepNames]);

        ToolRunContext context = new(run);

        // L'écran est rendu à l'outil avant qu'il pose sa première question. La coquille nettoie au
        // retour au choix d'onglet, et nulle part ailleurs : sans ceci, les invites s'empilaient sous le
        // bandeau et le menu, et le développeur lisait ses questions au milieu de ce qu'il venait de
        // quitter. Varcompare le faisait déjà ; apidelta ne le faisait nulle part.
        _console.Clear();

        // Une exécution interrompue est proposée avant toute lecture. Seule la sélection revient, et
        // seulement si les fichiers sont encore ceux qui avaient été lus.
        await _resume.TryResumeAsync(Id, context, cancellationToken).ConfigureAwait(false);

        // Les effets du choix de reprise et ses éventuels refus doivent rester lisibles.
        await _runner.RunAsync(context, _steps, cancellationToken).ConfigureAwait(false);

        RenderOutcome(run, context);
        ResumePrompt.RenderCleanupWarning(_console, context);

        if (run.Outcome != RunOutcome.Success)
        {
            if (run.Outcome is not RunOutcome.Cancelled || context.Get<RunSelection>(ApiDeltaContextKeys.Selection) is not null)
            {
                Pause();
            }

            return run;
        }

        Consult(context, cancellationToken);

        return run;
    }

    private void Consult(ToolRunContext context, CancellationToken cancellationToken)
    {
        if (context.Get<IReadOnlyList<Difference>>(ApiDeltaContextKeys.Differences) is { } differences)
        {
            _comparison.Show(
                differences,
                context.Get<IReadOnlyList<Artifact>>(ApiDeltaContextKeys.Artifacts) ?? [],
                context.Get<VersionRecommendation>(ApiDeltaContextKeys.VersionRecommendation),
                cancellationToken);
        }

        bool finishedThroughMenu = false;
        if (context.Get<AuditResult>(ApiDeltaContextKeys.Audit) is { } audit)
        {
            _findings.Show(audit, cancellationToken,
                hasNextView: context.Get<OptimizationPlan>(ApiDeltaContextKeys.Plan) is not null);
            finishedThroughMenu = audit.Findings.Count > 0;
        }

        if (context.Get<OptimizationPlan>(ApiDeltaContextKeys.Plan) is { } plan
            && context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument) is { } production)
        {
            _proposals.Show(
                plan,
                production.ByteLength,
                context.Get<OptimizationOutcome>(ApiDeltaContextKeys.Optimization),
                cancellationToken);
            finishedThroughMenu = plan.Groups.Count > 0;
        }

        if (!finishedThroughMenu)
        {
            Pause();
        }
    }

    private void RenderOutcome(ToolRun run, ToolRunContext context)
    {
        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);

        if (selection is not null)
        {
            _summary.Render(selection, run);
            _diagnostics.Render(DiagnosticCollector.Of(context), selection.ProductionPath);
        }

        RenderCode(context);

        if (run.Outcome != RunOutcome.Success)
        {
            StepState? failed = run.Steps.FirstOrDefault(step => step.Status == StepStatus.Failed);

            _console.MarkupLine(failed?.FailureReason is { } reason
                ? $"[red]{Markup.Escape(failed.Name)} ne s'est pas achevée :[/] {Markup.Escape(reason)}"
                : "[yellow]L'exécution ne s'est pas achevée.[/]");
        }
        else if (run.Steps.Any(step => step.Status == StepStatus.Skipped && step.FailureReason is not null))
        {
            _console.MarkupLine("[yellow]Le traitement a continué après une erreur. Des fichiers ont pu être écrits avant celle-ci ; vérifiez le dossier des résultats.[/]");
        }
        else if (selection is not null)
        {
            _summary.RenderWritten(selection,
                context.Get<IReadOnlyList<Artifact>>(ApiDeltaContextKeys.Artifacts) ?? []);
        }
    }

    private void Pause()
    {
        _console.MarkupLine("[bold cyan]Entrée : revenir au menu OpenAPI.[/]");
        _console.Input.ReadKey(intercept: true);
    }

    /// <summary>
    /// Explique le résultat en français ; les codes des rapports restent inchangés.
    /// </summary>
    /// <remarks>
    /// Affichée et écrite dans le rapport lisible par une machine, et nulle part ailleurs. Ce n'est pas
    /// le code de sortie du processus : la coquille garde les siens, et cette livraison n'en ajoute
    /// aucun. Le vocabulaire est fixé maintenant pour qu'une invocation automatisée, quand elle sera
    /// spécifiée comme sa propre fonctionnalité, ajoute une surface sans changer un sens.
    /// </remarks>
    private void RenderCode(ToolRunContext context)
    {
        if (context.Get<string>(ApiDeltaContextKeys.Outcome) is not { Length: > 0 } code)
        {
            return;
        }

        if (context.Run.Steps.Any(step => step.Status == StepStatus.Skipped && step.FailureReason is not null))
        {
            _console.MarkupLine("[yellow]Une étape a été passée après une erreur : les résultats peuvent être incomplets.[/]");
            return;
        }

        string label = code switch
        {
            "clean" => "Aucun problème au-dessus du seuil choisi.",
            "thresholdExceeded" => "Des points à vérifier dépassent le seuil choisi.",
            "breakingChange" => "Des changements incompatibles avec l’ancien contrat ont été détectés.",
            "invalidUsage" => "La demande est incomplète ou invalide.",
            "loadFailure" => "Un fichier n’a pas pu être lu correctement.",
            "outputFailure" => "Un résultat n’a pas pu être écrit ou vérifié.",
            _ => "Une erreur interne a interrompu le traitement.",
        };

        _console.MarkupLine(
            string.Equals(code, OutcomeCodeNames.Of(OutcomeCode.Clean), StringComparison.Ordinal)
                ? "[grey]" + label + "[/]"
                : "[yellow]" + label + "[/]");
    }
}
