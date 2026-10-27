using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Optimization;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Chiffre ce que le nettoyage rapporterait, sans rien nettoyer.
/// </summary>
/// <remarks>
/// L'étape ne touche à rien, et c'est tout son propos : un développeur doit pouvoir décider d'un
/// nettoyage avant d'en faire un. Elle ne relit pas le fichier et ne refait pas l'examen — elle mesure ce
/// que l'examen a établi, de sorte qu'un chiffre affiché ici ne peut pas contredire un constat affiché
/// là.
/// <para>
/// Rejouable : mesurer un arbre déjà lu ne touche à rien et donne deux fois le même résultat.
/// </para>
/// </remarks>
public sealed class PlanStep : IToolStep
{
    private readonly ApiDeltaOptions _options;

    /// <summary>Crée l'étape.</summary>
    /// <param name="options">Ce que la racine de composition fixe, dont le jeu de règles par défaut.</param>
    public PlanStep(ApiDeltaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Plan;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        SpecDocument? production = context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument);
        AuditResult? audit = context.Get<AuditResult>(ApiDeltaContextKeys.Audit);

        if (production is null || audit is null)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput,
                "Il n'y a rien à chiffrer : le fichier de production n'a pas été examiné."));
        }

        context.Set(
            ApiDeltaContextKeys.Plan,
            TransformationPlanner.Plan(
                production,
                audit,
                context.Get<Ruleset>(ApiDeltaContextKeys.Ruleset) ?? _options.DefaultRuleset));

        return Task.FromResult(Result.Success());
    }
}
