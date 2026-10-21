using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>Lit la description candidate.</summary>
/// <remarks>
/// Sautée lorsque le mode n'en comporte pas : un audit, une optimisation et une extraction portent sur une
/// seule description.
/// </remarks>
public sealed class ReadSourceStep : IToolStep
{
    private readonly ISpecReader _reader;

    /// <summary>Crée l'étape.</summary>
    /// <param name="reader">Lit la description.</param>
    public ReadSourceStep(ISpecReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.ReadSource;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);

        if (selection?.CandidatePath is not { } candidate)
        {
            return Result.Fail(
                FailureReason.InvalidInput, "Ce mode compare deux descriptions, et la candidate manque.");
        }

        SpecReadResult result = await _reader
            .ReadAsync(candidate, DocumentRole.Candidate, selection.Limits, cancellationToken)
            .ConfigureAwait(false);

        DiagnosticCollector.Add(context, result.Diagnostics);

        if (!result.IsFaithful)
        {
            return Result.Fail(
                FailureReason.InvalidInput,
                "La description candidate n'a pas pu être lue fidèlement : "
                    + DiagnosticCollector.Summarise(result.Blocking));
        }

        context.Set(ApiDeltaContextKeys.CandidateDocument, result.Document!);

        return Result.Success();
    }
}
