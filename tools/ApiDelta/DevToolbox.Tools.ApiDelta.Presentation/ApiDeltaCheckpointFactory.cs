using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Steps;

namespace DevToolbox.Tools.ApiDelta.Presentation;

/// <summary>
/// Construit ce qui survit au processus pour une exécution apidelta interrompue.
/// </summary>
/// <remarks>
/// Noter ce que cette fabrique lit dans le contexte et ce qu'elle ignore : le mode, les deux chemins avec
/// leurs empreintes, le dossier de sortie, le jeu de règles et les opérations retenues. Elle ne touche
/// jamais <see cref="ApiDeltaContextKeys.ProductionDocument"/> ni
/// <see cref="ApiDeltaContextKeys.CandidateDocument"/>, où résident les documents. C'est là tout le
/// mécanisme qui garantit qu'aucun contenu de description ne survit à la session.
/// <para>
/// L'empreinte accompagne le chemin parce qu'elle est ce qui rend la reprise vérifiable : relire un
/// fichier modifié depuis donnerait une réponse assurée au sujet d'un document qui n'existe plus.
/// </para>
/// </remarks>
public sealed class ApiDeltaCheckpointFactory : IRunCheckpointFactory
{
    private readonly IClock _clock;

    /// <summary>Crée la fabrique.</summary>
    /// <param name="clock">Fournit l'horodatage de dernière mise à jour, qui pilote la péremption.</param>
    public ApiDeltaCheckpointFactory(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <inheritdoc />
    public string ToolId => "apidelta";

    /// <inheritdoc />
    public PersistedRun? TryCreate(ToolRunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);

        if (selection is null)
        {
            // Tant que rien n'est choisi, il n'y a rien qui vaille d'être repris.
            return null;
        }

        return new PersistedRun(
            context.Run.RunId,
            context.Run.ToolId,
            Describe(selection),
            context.Run.GetCompletedStepNames(),
            context.Run.StartedAt,
            _clock.UtcNow);
    }

    /// <summary>Projette la sélection en éléments de point de reprise : trois chaînes chacun.</summary>
    private static List<PersistedSelectionEntry> Describe(RunSelection selection)
    {
        List<PersistedSelectionEntry> entries =
        [
            new(ApiDeltaSelectionKinds.Mode, selection.Mode.ToString(), selection.Mode.ToString()),
            new(
                ApiDeltaSelectionKinds.Production,
                selection.ProductionPath,
                Label(selection.ProductionPath, selection.ProductionHash)),
            new(ApiDeltaSelectionKinds.Output, selection.OutputDirectory, selection.OutputDirectory),
        ];

        if (!string.IsNullOrWhiteSpace(selection.CandidatePath))
        {
            entries.Add(new PersistedSelectionEntry(
                ApiDeltaSelectionKinds.Candidate,
                selection.CandidatePath,
                Label(selection.CandidatePath, selection.CandidateHash)));
        }

        if (!string.IsNullOrWhiteSpace(selection.RulesetPath))
        {
            entries.Add(new PersistedSelectionEntry(
                ApiDeltaSelectionKinds.Ruleset, selection.RulesetPath, selection.RulesetPath));
        }

        entries.AddRange(selection.SelectedOperations.Select(operation => new PersistedSelectionEntry(
            ApiDeltaSelectionKinds.Operation, operation.Display, operation.Display)));

        if (selection.Correction is { } correction)
        {
            entries.AddRange(correction.Routes.Select(route => new PersistedSelectionEntry(
                ApiDeltaSelectionKinds.CorrectedRoute, route.Display, route.Display)));

            entries.AddRange(correction.Differences.Select(difference => new PersistedSelectionEntry(
                ApiDeltaSelectionKinds.CorrectedDifference,
                difference.Display,
                difference.Display)));
        }

        if (selection.Correspondence is { IsConfirmed: true } correspondence)
        {
            // Seules les entrées confirmées sont retenues. Une proposition reconduite d'une séance à
            // l'autre serait une correspondance que l'outil se serait donnée à lui-même, et reprendre
            // relit : seule la décision du développeur survit.
            entries.AddRange(correspondence.Comparable.Select(entry => new PersistedSelectionEntry(
                ApiDeltaSelectionKinds.Correspondence,
                entry.Operation + " → " + entry.Route,
                entry.Operation + " → " + entry.Route)));
        }

        return entries;
    }

    /// <summary>
    /// Nomme un fichier et le début de son empreinte : assez pour que l'invite de reprise dise de quoi il
    /// s'agit, sans étaler soixante-quatre caractères sur un écran.
    /// </summary>
    private static string Label(string path, string? hash) =>
        string.IsNullOrWhiteSpace(hash)
            ? Path.GetFileName(path)
            : Path.GetFileName(path) + " · sha256:" + hash[..Math.Min(12, hash.Length)];

    /// <summary>Relit une sélection mémorisée.</summary>
    /// <param name="run">L'exécution mémorisée.</param>
    /// <returns>La sélection, ou <see langword="null"/> si elle est illisible.</returns>
    /// <remarks>
    /// La traduction a lieu ici, à la frontière de l'outil : un point de reprise ne porte que des chaînes,
    /// et c'est apidelta qui sait que l'une d'elles est un mode et l'autre un chemin. Une sélection
    /// illisible — écrite par une version antérieure, ou tronquée — ne fait pas échouer la reprise : elle
    /// la rend simplement indisponible.
    /// </remarks>
    public static RunSelection? TryRead(PersistedRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        string? mode = Key(run, ApiDeltaSelectionKinds.Mode);
        string? production = Key(run, ApiDeltaSelectionKinds.Production);
        string? output = Key(run, ApiDeltaSelectionKinds.Output);

        if (mode is null || production is null || output is null
            || !Enum.TryParse(mode, out RunMode parsed))
        {
            return null;
        }

        IReadOnlyList<OperationSelector> operations =
        [
            .. run.Selection
                .Where(entry => string.Equals(
                    entry.Kind, ApiDeltaSelectionKinds.Operation, StringComparison.Ordinal))
                .Select(entry => OperationSelector.TryParse(entry.Key, out OperationSelector? selector)
                    ? selector
                    : null)
                .OfType<OperationSelector>(),
        ];

        IReadOnlyList<OperationSelector> corrected =
        [
            .. Of(run, ApiDeltaSelectionKinds.CorrectedRoute)
                .Select(key => OperationSelector.TryParse(key, out OperationSelector? selector)
                    ? selector
                    : null)
                .OfType<OperationSelector>(),
        ];

        IReadOnlyList<DifferenceSelector> reported =
        [
            .. Of(run, ApiDeltaSelectionKinds.CorrectedDifference)
                .Select(key => DifferenceSelector.TryParse(key, out DifferenceSelector? selector)
                    ? selector
                    : null)
                .OfType<DifferenceSelector>(),
        ];

        return new RunSelection(
            parsed,
            production,
            // Les empreintes sont reprises du point de reprise, puis confrontées à celles des fichiers
            // relus. C'est cette confrontation qui refuse une reprise devenue mensongère.
            HashOf(run, ApiDeltaSelectionKinds.Production) ?? string.Empty,
            output,
            CandidatePath: Key(run, ApiDeltaSelectionKinds.Candidate),
            CandidateHash: HashOf(run, ApiDeltaSelectionKinds.Candidate),
            RulesetPath: Key(run, ApiDeltaSelectionKinds.Ruleset),
            Correction: corrected.Count > 0 || reported.Count > 0
                ? new CorrectionSelection(corrected, reported)
                : null)
        {
            SelectedOperations = operations,
        };
    }

    /// <summary>Les clés d'une sorte donnée, dans l'ordre où elles ont été écrites.</summary>
    private static IEnumerable<string> Of(PersistedRun run, string kind) =>
        run.Selection
            .Where(entry => string.Equals(entry.Kind, kind, StringComparison.Ordinal))
            .Select(entry => entry.Key);

    private static string? Key(PersistedRun run, string kind) =>
        run.Selection
            .FirstOrDefault(entry => string.Equals(entry.Kind, kind, StringComparison.Ordinal))
            ?.Key;

    /// <summary>Relit l'empreinte abrégée que <see cref="Label"/> avait inscrite.</summary>
    private static string? HashOf(PersistedRun run, string kind)
    {
        string? label = run.Selection
            .FirstOrDefault(entry => string.Equals(entry.Kind, kind, StringComparison.Ordinal))
            ?.Label;

        const string marker = "sha256:";
        int index = label?.IndexOf(marker, StringComparison.Ordinal) ?? -1;

        return index < 0 ? null : label![(index + marker.Length)..];
    }
}
