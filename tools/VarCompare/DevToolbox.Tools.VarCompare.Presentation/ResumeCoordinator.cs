using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Core.Steps;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation;

/// <summary>
/// Restaure la <em>sélection</em> d'une exécution interrompue afin qu'elle puisse se poursuivre.
/// </summary>
/// <remarks>
/// <para>
/// La distinction que cette classe fait respecter : ce qui est restauré, c'est la collection, le projet, les
/// groupes choisis et les noms des étapes achevées, jamais le contenu de ces groupes. L'étape de lecture
/// reste volontairement en attente, de sorte qu'une reprise relit et que le développeur n'agit jamais sur
/// une image périmée.
/// </para>
/// <para>
/// Un groupe mémorisé qui a depuis disparu ou est devenu illisible est écarté avec une explication, plutôt
/// que de faire échouer la reprise.
/// </para>
/// </remarks>
public sealed class ResumeCoordinator
{
    private readonly ResumePrompt _prompt;
    private readonly IRunCheckpointStore _store;
    private readonly IAnsiConsole _console;

    /// <summary>Crée le coordinateur.</summary>
    /// <param name="prompt">Demande s'il faut reprendre.</param>
    /// <param name="store">Lit et oublie les exécutions mémorisées.</param>
    /// <param name="console">Explique ce qui a été écarté.</param>
    public ResumeCoordinator(ResumePrompt prompt, IRunCheckpointStore store, IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(console);

        _prompt = prompt;
        _store = store;
        _console = console;
    }

    /// <summary>Propose une exécution interrompue et, en cas de reprise, amorce le contexte avec sa sélection.</summary>
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

        Seed(context, answer.Run);

        return true;
    }

    /// <summary>
    /// Écarte les groupes mémorisés qui ne figurent plus dans le projet, en disant lesquels et pourquoi.
    /// </summary>
    /// <param name="remembered">Les groupes que l'exécution interrompue avait retenus.</param>
    /// <param name="available">Les groupes lisibles dans le projet aujourd'hui.</param>
    /// <returns>Les groupes encore comparables.</returns>
    public IReadOnlyList<VariableGroupSummary> ReconcileSelection(
        IReadOnlyList<PersistedGroupSelection> remembered,
        IReadOnlyList<VariableGroupSummary> available)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        ArgumentNullException.ThrowIfNull(available);

        Dictionary<int, VariableGroupSummary> byId = available.ToDictionary(group => group.Id);

        List<VariableGroupSummary> kept = [];
        List<string> dropped = [];

        foreach (PersistedGroupSelection selection in remembered)
        {
            if (byId.TryGetValue(selection.Id, out VariableGroupSummary? group))
            {
                kept.Add(group);
            }
            else
            {
                dropped.Add(selection.Name);
            }
        }

        if (dropped.Count > 0)
        {
            // Le dire, et poursuivre avec ce qui reste plutôt que de refuser la reprise.
            _console.MarkupLine(
                "[yellow]These groups are no longer there, or you can no longer read them: "
                + Markup.Escape(string.Join(", ", dropped))
                + ". Continuing with the rest.[/]");
        }

        return kept;
    }

    /// <summary>
    /// Restaure la sélection dans le contexte, en laissant la lecture en attente pour que les groupes soient
    /// relus.
    /// </summary>
    private static void Seed(ToolRunContext context, PersistedRun run)
    {
        context.Set(VarCompareContextKeys.Project, new ProjectIdentifier(run.Project));
        context.Set(VarCompareContextKeys.ResumedSelection, run.SelectedGroups);

        // Les étapes déjà achevées sont marquées pour ne pas être refaites, à l'exception de la lecture, qui
        // doit être rejouée quoi que dise le point de reprise.
        foreach (string completed in run.CompletedSteps)
        {
            if (string.Equals(completed, RetrieveGroupsStep.StepName, StringComparison.Ordinal)
                || string.Equals(completed, BuildComparisonStep.StepName, StringComparison.Ordinal))
            {
                continue;
            }

            StepState? step = context.Run.FindStep(completed);

            if (step is not null && step.Status == StepStatus.Pending)
            {
                step.Start(run.LastUpdatedAt);
                step.Complete(run.LastUpdatedAt);
            }
        }
    }
}
