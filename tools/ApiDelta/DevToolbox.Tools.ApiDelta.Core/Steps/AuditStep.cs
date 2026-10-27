using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Examine la description de production, seule.
/// </summary>
/// <remarks>
/// Seule, et c'est dans le sujet : l'audit ne compare rien. Il dit ce que le fichier de production a
/// accumulé, et ce constat ne dépend d'aucun candidat — ce qui permet de l'exécuter quand il n'y en a pas.
/// <para>
/// Rejouable : parcourir un arbre déjà lu ne touche à rien et donne deux fois le même résultat.
/// </para>
/// </remarks>
public sealed class AuditStep : IToolStep
{
    private readonly IAuditEngine _engine;
    private readonly ApiDeltaOptions _options;
    private readonly IClock _clock;

    /// <summary>Crée l'étape.</summary>
    /// <param name="engine">Fait tourner les règles.</param>
    /// <param name="options">Ce que la racine de composition fixe, dont le jeu de règles par défaut.</param>
    /// <param name="clock">Fournit le jour, pour les échéances des mises en sourdine.</param>
    public AuditStep(IAuditEngine engine, ApiDeltaOptions options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _engine = engine;
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Audit;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        SpecDocument? production = context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument);

        if (production is null)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput, "Il n'y a rien à examiner : aucune description n'a été lue."));
        }

        // Le jeu de règles vient de la sélection, qui l'a lu et refusé de démarrer s'il n'était pas
        // applicable. Retomber ici sur les valeurs livrées appliquerait une configuration que personne
        // n'a écrite.
        Ruleset ruleset = context.Get<Ruleset>(ApiDeltaContextKeys.Ruleset)
            ?? _options.DefaultRuleset;

        AuditResult result = _engine.Run(
            new AuditRequest(
                production,
                ruleset,
                context.Get<ReferenceGraphResult>(ApiDeltaContextKeys.ReferenceGraph),
                context.Get<IReadOnlyList<Diagnostic>>(ApiDeltaContextKeys.StructuralVerdicts)),
            cancellationToken);

        context.Set(ApiDeltaContextKeys.Audit, Silenced(context, result));

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Applique les mises en sourdine aux constats, et garde trace de ce qu'elles ont tu.
    /// </summary>
    /// <remarks>
    /// Les sourdines sont appliquées après les règles et non pendant : une règle ne doit pas savoir
    /// qu'on la fait taire quelque part, sans quoi « la règle n'a rien trouvé » et « on lui a demandé de
    /// se taire » cesseraient d'être distinguables. Le sort de la règle continue donc de compter ce
    /// qu'elle a trouvé, et la liste des constats ne porte que ce qui reste à traiter.
    /// </remarks>
    private AuditResult Silenced(ToolRunContext context, AuditResult result)
    {
        if (context.Get<IReadOnlyList<Suppression>>(ApiDeltaContextKeys.Suppressions)
            is not { Count: > 0 } suppressions)
        {
            return result;
        }

        SuppressionOutcome outcome = SuppressionApplier.Apply(
            result.Findings, suppressions, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));

        return result with { Findings = outcome.Findings, Suppressions = outcome };
    }
}
