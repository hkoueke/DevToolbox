using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>Compare le candidat à la production.</summary>
/// <remarks>
/// Rejouable : comparer deux arbres déjà lus ne touche à rien et donne deux fois le même résultat.
/// </remarks>
public sealed class CompareStep : IToolStep
{
    private readonly IDocumentComparer _comparer;

    /// <summary>Crée l'étape.</summary>
    /// <param name="comparer">Compare les deux descriptions.</param>
    public CompareStep(IDocumentComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        _comparer = comparer;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Compare;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        SpecDocument? production = context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument);
        SpecDocument? candidate = context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument);

        if (production is null || candidate is null)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput,
                "La comparaison demande deux descriptions lues, et il en manque une."));
        }

        Result<IReadOnlyList<Difference>> compared =
            _comparer.Compare(candidate, production, cancellationToken);

        if (compared.IsFailure)
        {
            return Task.FromResult(Result.Fail(compared.Failure!));
        }

        context.Set(ApiDeltaContextKeys.Differences, compared.Value);

        return Task.FromResult(Result.Success());
    }
}
