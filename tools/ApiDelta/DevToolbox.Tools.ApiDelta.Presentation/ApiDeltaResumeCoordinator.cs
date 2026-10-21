using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation;

/// <summary>
/// Restaure la <em>sélection</em> d'une exécution interrompue, après avoir vérifié qu'elle veut encore
/// dire quelque chose.
/// </summary>
/// <remarks>
/// Ce qui est restauré : le mode, les chemins, le dossier de sortie, le jeu de règles, les opérations
/// retenues. Jamais le contenu des descriptions, qui sera relu.
/// <para>
/// La vérification est ce qui distingue cette reprise d'une simple commodité : chaque fichier est
/// réempreinté et confronté à l'empreinte mémorisée. S'il a changé, la reprise est refusée en nommant le
/// fichier, car une comparaison reprise sur un document modifié depuis serait une réponse assurée au sujet
/// de ce qui n'existe plus.
/// </para>
/// </remarks>
public sealed class ApiDeltaResumeCoordinator
{
    private readonly ResumePrompt _prompt;
    private readonly IRunCheckpointStore _store;
    private readonly ISpecFileProbe _probe;
    private readonly IAnsiConsole _console;

    /// <summary>Crée le coordinateur.</summary>
    /// <param name="prompt">Demande s'il faut reprendre.</param>
    /// <param name="store">Lit et oublie les exécutions mémorisées.</param>
    /// <param name="probe">Réempreinte les fichiers mémorisés.</param>
    /// <param name="console">Explique ce qui a été refusé.</param>
    public ApiDeltaResumeCoordinator(
        ResumePrompt prompt,
        IRunCheckpointStore store,
        ISpecFileProbe probe,
        IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(console);

        _prompt = prompt;
        _store = store;
        _probe = probe;
        _console = console;
    }

    /// <summary>Propose une exécution interrompue et, en cas de reprise, amorce le contexte.</summary>
    /// <param name="toolId">L'outil dont il faut chercher les exécutions.</param>
    /// <param name="context">Le contexte à amorcer.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns><see langword="true"/> si une exécution a été reprise.</returns>
    public async Task<bool> TryResumeAsync(
        string toolId,
        ToolRunContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<PersistedRun> candidates =
            await _store.ListResumableAsync(toolId, cancellationToken).ConfigureAwait(false);

        ResumeAnswer answer = _prompt.Ask(candidates);

        if (answer.Decision == ResumeDecision.Discard && answer.Run is not null)
        {
            await _store.DiscardAsync(answer.Run.RunId, cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (answer.Decision != ResumeDecision.Resume || answer.Run is null)
        {
            return false;
        }

        RunSelection? selection = ApiDeltaCheckpointFactory.TryRead(answer.Run);

        if (selection is null)
        {
            _console.MarkupLine(
                "[yellow]Ce point de reprise n'est pas lisible par cette version. L'exécution repart de "
                + "zéro.[/]");

            return false;
        }

        return await SeedIfUnchangedAsync(context, selection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Amorce le contexte si, et seulement si, les fichiers mémorisés sont encore ceux qui avaient été lus.
    /// </summary>
    private async Task<bool> SeedIfUnchangedAsync(
        ToolRunContext context,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        if (!await IsUnchangedAsync(
                selection.ProductionPath, selection.ProductionHash, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (selection.CandidatePath is { } candidate
            && !await IsUnchangedAsync(
                candidate, selection.CandidateHash, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        context.Set(ApiDeltaContextKeys.ResumedSelection, selection);

        return true;
    }

    /// <summary>
    /// Établit qu'un fichier mémorisé est encore celui qui avait été lu.
    /// </summary>
    /// <remarks>
    /// L'empreinte mémorisée est abrégée pour l'affichage : la comparaison porte donc sur son préfixe, ce
    /// qui suffit amplement à distinguer deux versions d'un même fichier.
    /// </remarks>
    private async Task<bool> IsUnchangedAsync(
        string path,
        string? rememberedHash,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rememberedHash))
        {
            return true;
        }

        Result<SpecFileFacts> facts = await _probe
            .ProbeAsync(path, long.MaxValue, cancellationToken).ConfigureAwait(false);

        if (facts.IsFailure)
        {
            _console.MarkupLine(
                "[yellow]" + Markup.Escape(Path.GetFileName(path))
                + " ne peut plus être lu : " + Markup.Escape(facts.Failure!.Message)
                + " L'exécution repart de zéro.[/]");

            return false;
        }

        if (facts.Value.Sha256.StartsWith(rememberedHash, StringComparison.Ordinal))
        {
            return true;
        }

        _console.MarkupLine(
            "[yellow]" + Markup.Escape(Path.GetFileName(path))
            + " a été modifié depuis. Reprendre donnerait un résultat portant sur une version du "
            + "fichier qui n'existe plus : on recommence depuis le début.[/]");

        return false;
    }
}
