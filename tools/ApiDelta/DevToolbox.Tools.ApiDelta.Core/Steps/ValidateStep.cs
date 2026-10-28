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

        List<Diagnostic> onProduction = [];

        Inspect(production, selection, onProduction, context);

        List<Diagnostic> onCompanion = [];

        if (context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument) is { } candidate)
        {
            Inspect(candidate, selection, onCompanion, null);
        }

        DiagnosticCollector.Add(context, onProduction);
        DiagnosticCollector.Add(context, onCompanion);

        List<Diagnostic> blocking = [.. onProduction.Where(diagnostic => diagnostic.IsBlocking)];

        // Seuls les verdicts sur la production arrêtent l'exécution, et la raison est celle de tout cet
        // outil : on ne répond que de ce qu'on écrit. La production est recopiée puis republiée, donc
        // elle doit tenir. Le fichier compagnon, lui, n'est jamais publié — on y prend ce que le
        // développeur désigne, et le document consolidé fait l'objet de sa propre vérification.
        //
        // Les refuser ensemble rendait l'outil inutilisable sur les fichiers qui existent vraiment : un
        // export Swagger dont « info » est vide est le cas ordinaire, et aucune comparaison de routes ne
        // dépend de « info.title ». Les plaintes sur le compagnon sont consignées, nommées sous son
        // propre nom à l'écran, et portées au rapport — elles ne sont ni perdues ni fatales.
        if (blocking.Count > 0)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput,
                "La validation a refusé votre fichier de production : "
                    + DiagnosticCollector.Summarise(blocking)));
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
        // Chaque verdict est timbré du document qu'il regarde. Les deux fichiers sont inspectés dans la
        // même liste, et sans ce timbre une plainte sur le compagnon ressortait sous le nom de la
        // production : « le champ "title" est requis » affiché sous un fichier qui avait un titre.
        IReadOnlyList<Diagnostic> verdicts =
        [
            .. _validator.Validate(document)
                .Select(verdict => verdict with { Document = document.Path }),
        ];

        found.AddRange(verdicts);

        List<Diagnostic> resolution = [];
        ReferenceGraphResult graph = _references.Resolve(document, selection.References, selection.Limits, resolution);

        found.AddRange(resolution.Select(one => one with { Document = document.Path }));

        context?.Set(ApiDeltaContextKeys.StructuralVerdicts, verdicts);
        context?.Set(ApiDeltaContextKeys.ReferenceGraph, graph);
    }
}
