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

        if (context.Get<Wsdl.WsdlContract>(ApiDeltaContextKeys.WsdlContract) is not null)
        {
            // Le compagnon est un contrat de service : l'étape précédente l'a lu, et l'appariement qui
            // suit en produira le document que la comparaison lira. Le lire ici comme une description
            // OpenAPI échouerait sur la première balise — et c'est ce qui arrivait tant que les deux
            // étapes du contrat n'étaient pas dans la chaîne.
            return Result.Success();
        }

        if (selection?.CandidatePath is not { } candidate)
        {
            return Result.Fail(
                FailureReason.InvalidInput,
                "Consolider demande un fichier compagnon, et il manque.");
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
