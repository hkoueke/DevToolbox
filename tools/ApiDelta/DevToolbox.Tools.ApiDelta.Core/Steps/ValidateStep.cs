using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Valide la structure des descriptions lues et résout leurs références.
/// </summary>
/// <remarks>
/// C'est ici que se joue la discipline du refus : une description qui ne respecte pas la structure de sa
/// version, ou qui emploie une référence morte, n'atteint pas la comparaison. Un rapport sûr de lui fondé
/// sur une description mal lue est indiscernable d'un bon rapport, ce qui en fait le pire des deux.
/// </remarks>
public sealed class ValidateStep : IToolStep
{
    private readonly ISpecValidator _validator;
    private readonly IReferenceGraph _references;

    /// <summary>Crée l'étape.</summary>
    /// <param name="validator">Valide la structure.</param>
    /// <param name="references">Résout les références.</param>
    public ValidateStep(ISpecValidator validator, IReferenceGraph references)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(references);

        _validator = validator;
        _references = references;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Validate;

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
                FailureReason.InvalidInput, "Aucune description lue : il n'y a rien à valider."));
        }

        List<Diagnostic> found = [];

        Inspect(production, selection, found, context);

        if (context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument) is { } candidate)
        {
            Inspect(candidate, selection, found, null);
        }

        DiagnosticCollector.Add(context, found);

        List<Diagnostic> blocking = [.. found.Where(diagnostic => diagnostic.IsBlocking)];

        if (blocking.Count > 0)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput,
                "La validation a refusé la description : " + DiagnosticCollector.Summarise(blocking)));
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Valide un document et résout ses références.
    /// </summary>
    /// <remarks>
    /// Le contexte n'est passé que pour la production : c'est elle que l'audit examine, et le graphe
    /// comme les verdicts y sont déposés pour qu'il n'ait pas à les recalculer. Les déposer veut dire
    /// quelque chose de précis — que l'analyse a bien eu lieu — et une règle qui ne les trouve pas se
    /// déclare non évaluée au lieu de conclure sans.
    /// </remarks>
    private void Inspect(
        SpecDocument document,
        RunSelection selection,
        List<Diagnostic> found,
        ToolRunContext? context)
    {
        IReadOnlyList<Diagnostic> verdicts = _validator.Validate(document);
        found.AddRange(verdicts);

        List<Diagnostic> resolution = [];
        ReferenceGraphResult graph = _references.Resolve(document, selection.References, resolution);
        found.AddRange(resolution);

        context?.Set(ApiDeltaContextKeys.StructuralVerdicts, verdicts);
        context?.Set(ApiDeltaContextKeys.ReferenceGraph, graph);
    }
}
