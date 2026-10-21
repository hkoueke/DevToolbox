using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Établit ce qu'il faut lire, ce qu'il faut en faire, et où écrire.
/// </summary>
/// <remarks>
/// C'est la seule étape dont le travail varie selon le mode au lieu d'être sautée par lui, puisque c'est
/// elle qui détermine le mode. Elle prend aussi l'empreinte de chaque fichier choisi : une sélection sans
/// empreinte serait reprenable sans être vérifiable, et reprendre une comparaison sur un fichier modifié
/// depuis est exactement l'erreur que la reprise doit rendre impossible.
/// </remarks>
public sealed class SelectStep : IToolStep
{
    private readonly IApiDeltaPrompt _prompt;
    private readonly ISpecFileProbe _probe;
    private readonly IRulesetStore _rulesets;
    private readonly ISuppressionStore _suppressions;
    private readonly ApiDeltaOptions _options;

    /// <summary>Crée l'étape.</summary>
    /// <param name="prompt">Demande au développeur ce qu'il veut.</param>
    /// <param name="probe">Établit que les fichiers choisis sont lisibles, et en prend l'empreinte.</param>
    /// <param name="rulesets">Lit le jeu de règles, ou refuse de démarrer.</param>
    /// <param name="suppressions">Lit les mises en sourdine, ou refuse de démarrer.</param>
    /// <param name="options">Ce que la racine de composition fixe, dont le jeu de règles livré.</param>
    public SelectStep(
        IApiDeltaPrompt prompt,
        ISpecFileProbe probe,
        IRulesetStore rulesets,
        ISuppressionStore suppressions,
        ApiDeltaOptions options)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(rulesets);
        ArgumentNullException.ThrowIfNull(suppressions);
        ArgumentNullException.ThrowIfNull(options);

        _prompt = prompt;
        _probe = probe;
        _rulesets = rulesets;
        _suppressions = suppressions;
        _options = options;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Select;

    /// <inheritdoc />
    /// <remarks>
    /// Rejouable : demander de nouveau ne change rien au monde, et c'est au développeur de répondre ce
    /// qu'il veut cette fois-ci.
    /// </remarks>
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? resumed = context.Get<RunSelection>(ApiDeltaContextKeys.ResumedSelection);

        Result<RunSelection> asked = _prompt.AskForSelection(resumed, cancellationToken);

        if (asked.IsFailure)
        {
            return Result.Fail(asked.Failure!.Reason, asked.Failure.Message);
        }

        Result<RunSelection> verified = await VerifyInputsAsync(asked.Value, cancellationToken)
            .ConfigureAwait(false);

        if (verified.IsFailure)
        {
            return Result.Fail(verified.Failure!.Reason, verified.Failure.Message);
        }

        Result configured = await ConfigureAsync(context, verified.Value, cancellationToken)
            .ConfigureAwait(false);

        if (configured.IsFailure)
        {
            return configured;
        }

        context.Set(ApiDeltaContextKeys.Selection, verified.Value);
        SkipStepsOutsideMode(context.Run, verified.Value.Mode);

        return Result.Success();
    }

    /// <summary>
    /// Lit le jeu de règles et les sourdines, et refuse de démarrer si l'un des deux n'est pas
    /// applicable.
    /// </summary>
    /// <remarks>
    /// Ici, et non plus tard. Une configuration invalide découverte en cours d'examen laisserait le choix
    /// entre deux mauvaises réponses : l'appliquer à moitié, ou l'ignorer. Refuser de démarrer est la
    /// troisième, et la seule qui dise la vérité — d'où l'absence de tout repli silencieux sur les
    /// valeurs livrées.
    /// <para>
    /// Les fichiers nommés par convention à côté de la description sont trouvés sans qu'on les désigne.
    /// Une équipe qui range son jeu de règles à côté de sa description n'a pas à le redire à chaque
    /// exécution.
    /// </para>
    /// </remarks>
    private async Task<Result> ConfigureAsync(
        ToolRunContext context,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        Ruleset ruleset = _options.DefaultRuleset;

        if (Named(selection.RulesetPath, _rulesets.Discover(selection.ProductionPath)) is { } path)
        {
            RulesetReadResult read = await _rulesets
                .ReadAsync(path, ruleset, cancellationToken).ConfigureAwait(false);

            if (!read.IsApplicable)
            {
                DiagnosticCollector.Add(context, read.Diagnostics);

                return Result.Fail(
                    FailureReason.InvalidConfiguration,
                    "Ce jeu de règles n'est pas applicable : "
                    + DiagnosticCollector.Summarise(read.Diagnostics));
            }

            ruleset = read.Ruleset!;
        }

        context.Set(ApiDeltaContextKeys.Ruleset, ruleset);

        return await SuppressionsAsync(context, selection, ruleset, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Lit les sourdines et les valide avant que rien n'ait commencé.</summary>
    private async Task<Result> SuppressionsAsync(
        ToolRunContext context,
        RunSelection selection,
        Ruleset ruleset,
        CancellationToken cancellationToken)
    {
        if (Named(selection.SuppressionsPath, _suppressions.Discover(selection.ProductionPath))
            is not { } path)
        {
            return Result.Success();
        }

        SuppressionReadResult read = await _suppressions
            .ReadAsync(path, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Diagnostic> invalid = read.IsApplicable
            ? SuppressionValidator.Validate(read.Suppressions!, ruleset, path)
            : read.Diagnostics;

        if (invalid.Count > 0)
        {
            DiagnosticCollector.Add(context, invalid);

            return Result.Fail(
                FailureReason.InvalidConfiguration,
                "Ces mises en sourdine ne sont pas applicables : "
                + DiagnosticCollector.Summarise(invalid));
        }

        context.Set(ApiDeltaContextKeys.Suppressions, read.Suppressions!);

        return Result.Success();
    }

    /// <summary>
    /// Établit que chaque fichier choisi est lisible et admissible, et complète la sélection de leurs
    /// empreintes.
    /// </summary>
    private async Task<Result<RunSelection>> VerifyInputsAsync(
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        Result<SpecFileFacts> production = await _probe
            .ProbeAsync(selection.ProductionPath, selection.Limits.MaxBytes, cancellationToken)
            .ConfigureAwait(false);

        if (production.IsFailure)
        {
            return Result.Fail<RunSelection>(production.Failure!.Reason, production.Failure.Message);
        }

        RunSelection verified = selection with
        {
            ProductionPath = production.Value.FullPath,
            ProductionHash = production.Value.Sha256,
        };

        if (!selection.NeedsCandidate)
        {
            return Result.Success(verified);
        }

        if (string.IsNullOrWhiteSpace(selection.CandidatePath))
        {
            return Result.Fail<RunSelection>(
                FailureReason.InvalidInput,
                "Ce mode compare deux descriptions, et la description candidate n'a pas été désignée.");
        }

        Result<SpecFileFacts> candidate = await _probe
            .ProbeAsync(selection.CandidatePath, selection.Limits.MaxBytes, cancellationToken)
            .ConfigureAwait(false);

        if (candidate.IsFailure)
        {
            return Result.Fail<RunSelection>(candidate.Failure!.Reason, candidate.Failure.Message);
        }

        return Result.Success(verified with
        {
            CandidatePath = candidate.Value.FullPath,
            CandidateHash = candidate.Value.Sha256,
        });
    }

    /// <summary>
    /// Le fichier désigné, à défaut celui trouvé par convention, à défaut rien.
    /// </summary>
    /// <remarks>
    /// Une chaîne vide n'est pas un chemin. La distinction compte parce qu'une sélection reprise porte
    /// des chaînes et non des absences, et qu'un chemin vide ouvert en lecture donnerait un refus de
    /// démarrer pour une configuration que personne n'a désignée.
    /// </remarks>
    private static string? Named(string? chosen, string? discovered)
    {
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            return chosen;
        }

        return string.IsNullOrWhiteSpace(discovered) ? null : discovered;
    }

    /// <summary>
    /// Marque sautées les étapes que le mode ne traverse pas, afin que le dérouleur passe outre et que
    /// l'exécution ne prétende pas les avoir accomplies.
    /// </summary>
    private static void SkipStepsOutsideMode(ToolRun run, RunMode mode)
    {
        foreach (StepState state in run.Steps)
        {
            if (!ApiDeltaStepNames.Includes(state.Name, mode) && state.Status == StepStatus.Pending)
            {
                state.Skip(DateTimeOffset.UtcNow);
            }
        }
    }
}
