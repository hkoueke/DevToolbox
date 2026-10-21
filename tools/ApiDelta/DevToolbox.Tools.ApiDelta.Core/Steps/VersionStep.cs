using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Versioning;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>Recommande la version à publier.</summary>
/// <remarks>
/// Sautée pour une extraction : un extrait est une vue d'une description, non un changement de celle-ci,
/// et faire bouger le numéro reviendrait à annoncer une publication là où il n'y a qu'un regard.
/// </remarks>
public sealed class VersionStep : IToolStep
{
    private readonly IVersionPolicy _policy;

    /// <summary>Crée l'étape.</summary>
    /// <param name="policy">Recommande le numéro.</param>
    public VersionStep(IVersionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Version;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);
        SpecDocument? production = context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument);

        if (selection is null || production is null)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput,
                "Aucune description lue : il n'y a pas de version à recommander."));
        }

        IReadOnlyList<Difference> differences =
            context.Get<IReadOnlyList<Difference>>(ApiDeltaContextKeys.Differences) ?? [];

        // La version imposée viendra de l'invite qui accompagne le correctif : c'est là qu'un développeur
        // a une raison d'en désigner une. Le moteur sait déjà la traiter.
        VersionRecommendation recommendation = _policy.Recommend(
            differences, production, VersioningPolicy.Default);

        context.Set(ApiDeltaContextKeys.VersionRecommendation, recommendation);

        return Task.FromResult(Result.Success());
    }
}
