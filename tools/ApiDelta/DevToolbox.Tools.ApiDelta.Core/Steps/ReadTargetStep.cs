using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>Lit la description de production.</summary>
/// <remarks>
/// Rejouable : lire un fichier ne change rien. Une reprise relit donc réellement, ce qui est exactement ce
/// qu'on attend d'elle — la description a pu changer entre-temps, et c'est à la relecture de le constater.
/// </remarks>
public sealed class ReadTargetStep : IToolStep
{
    private readonly ISpecReader _reader;

    /// <summary>Crée l'étape.</summary>
    /// <param name="reader">Lit la description.</param>
    public ReadTargetStep(ISpecReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.ReadTarget;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);

        if (selection is null)
        {
            return Result.Fail(
                FailureReason.InvalidInput, "Aucune sélection : il n'y a rien à lire.");
        }

        SpecReadResult result = await _reader
            .ReadAsync(selection.ProductionPath, DocumentRole.Production, selection.Limits, cancellationToken)
            .ConfigureAwait(false);

        DiagnosticCollector.Add(context, result.Diagnostics);

        if (!result.IsFaithful)
        {
            return Result.Fail(
                FailureReason.InvalidInput,
                "La description de production n'a pas pu être lue fidèlement : "
                    + DiagnosticCollector.Summarise(result.Blocking));
        }

        context.Set(ApiDeltaContextKeys.ProductionDocument, result.Document!);

        return Result.Success();
    }
}
