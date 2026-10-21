using System.Globalization;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
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
public sealed class ResumeCoordinator : IResumedSelectionReconciler
{
    /// <summary>
    /// Les étapes qu'une reprise peut déclarer achevées sans les rejouer : celles, et celles seules, dont
    /// <see cref="Seed"/> redépose lui-même le résultat dans le contexte.
    /// </summary>
    /// <remarks>
    /// La règle tient en une phrase : marquer une étape achevée, c'est promettre que ce qu'elle produit est
    /// déjà là. Or le contexte ne survit pas au processus et une étape sautée ne redépose rien. Toute étape
    /// dont le résultat ne figure pas dans cette liste doit donc être rejouée, sans quoi une étape ultérieure
    /// cherchera en vain ce que personne n'a déposé — et l'exécution reprise échouera immédiatement, faute
    /// de sélection, là même où la reprise devait éviter de tout recommencer.
    /// </remarks>
    private static readonly string[] StepsSeedRestores = [IdentifyProjectStep.StepName];

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
    /// Relit un élément de sélection mémorisé comme un groupe. Un élément illisible est écarté plutôt que
    /// de faire échouer la reprise : un point de reprise corrompu ou écrit par une version antérieure ne
    /// doit pas empêcher de travailler.
    /// </summary>
    private static RememberedGroup? ToRememberedGroup(PersistedSelectionEntry entry) =>
        int.TryParse(entry.Key, CultureInfo.InvariantCulture, out int id)
            ? new RememberedGroup(id, entry.Label)
            : null;

    /// <inheritdoc />
    public IReadOnlyList<VariableGroupSummary> Reconcile(
        IReadOnlyList<RememberedGroup> remembered,
        IReadOnlyList<VariableGroupSummary> available)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        ArgumentNullException.ThrowIfNull(available);

        Dictionary<int, VariableGroupSummary> byId = available.ToDictionary(group => group.Id);

        List<VariableGroupSummary> kept = [];
        List<string> dropped = [];

        foreach (RememberedGroup selection in remembered)
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
                "[yellow]Ces groupes ne sont plus là, ou vous ne pouvez plus les lire : "
                + Markup.Escape(string.Join(", ", dropped))
                + ". La suite continue avec les autres.[/]");
        }

        return kept;
    }

    /// <summary>
    /// Restaure la sélection dans le contexte, en laissant tout le reste en attente pour que les groupes
    /// soient réellement relus.
    /// </summary>
    private static void Seed(ToolRunContext context, PersistedRun run)
    {
        // La traduction a lieu ici, à la frontière de l'outil : un point de reprise ne porte que des
        // chaînes, et c'est varcompare qui sait que « 42 » est un identifiant de groupe.
        string project = run.Selection
            .FirstOrDefault(entry => string.Equals(entry.Kind, VarCompareSelectionKinds.Project, StringComparison.Ordinal))
            ?.Key ?? string.Empty;

        IReadOnlyList<RememberedGroup> groups =
        [
            .. run.Selection
                .Where(entry => string.Equals(entry.Kind, VarCompareSelectionKinds.Group, StringComparison.Ordinal))
                .Select(ToRememberedGroup)
                .OfType<RememberedGroup>(),
        ];

        context.Set(VarCompareContextKeys.Project, new ProjectIdentifier(project));
        context.Set(VarCompareContextKeys.ResumedSelection, groups);

        foreach (string completed in run.CompletedSteps)
        {
            if (!StepsSeedRestores.Contains(completed, StringComparer.Ordinal))
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
