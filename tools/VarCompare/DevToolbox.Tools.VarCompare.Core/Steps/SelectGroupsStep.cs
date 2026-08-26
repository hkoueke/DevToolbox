using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Steps;

/// <summary>Étape 3 : retenir au moins deux groupes à comparer.</summary>
public sealed class SelectGroupsStep : IToolStep
{
    /// <summary>Le nom sous lequel cette étape apparaît dans les journaux et l'invite de reprise.</summary>
    public const string StepName = "SelectGroups";

    private readonly IGroupChooser _chooser;
    private readonly IResumedSelectionReconciler _reconciler;

    /// <summary>Crée l'étape.</summary>
    /// <param name="chooser">Demande au développeur quels groupes comparer.</param>
    /// <param name="reconciler">
    /// Rapproche une sélection reprise de ce qui est lisible aujourd'hui, en disant ce qui a été écarté.
    /// </param>
    public SelectGroupsStep(IGroupChooser chooser, IResumedSelectionReconciler reconciler)
    {
        ArgumentNullException.ThrowIfNull(chooser);
        ArgumentNullException.ThrowIfNull(reconciler);

        _chooser = chooser;
        _reconciler = reconciler;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<VariableGroupSummary>? available =
            context.Get<IReadOnlyList<VariableGroupSummary>>(VarCompareContextKeys.AvailableGroups);

        if (available is null)
        {
            return Result.Fail(FailureReason.InvalidConfiguration, "La liste des groupes n'a pas encore été lue.");
        }

        if (available.Count < VariableComparison.MinimumGroups)
        {
            // Le dire vaut mieux que d'afficher un tableau sans intérêt.
            return Result.Fail(
                FailureReason.GroupNotFound,
                available.Count == 0
                    ? "Aucun groupe de variables lisible n'a été trouvé dans ce projet."
                    : "Ce projet ne compte qu'un seul groupe de variables, or une comparaison en demande "
                        + "au moins deux.");
        }

        // Une exécution reprise sait déjà ce qui avait été choisi, et redemander serait une cérémonie inutile.
        // Ce qu'elle ignore, c'est le contenu des groupes, que l'étape de lecture relit de toute façon.
        IReadOnlyList<VariableGroupSummary>? resumed = ResolveResumedSelection(context, available);

        if (resumed is not null)
        {
            context.Set(VarCompareContextKeys.SelectedGroups, resumed);
            return Result.Success();
        }

        IReadOnlyList<VariableGroupSummary> selected =
            await _chooser.ChooseAsync(available, cancellationToken).ConfigureAwait(false);

        if (selected.Count < VariableComparison.MinimumGroups)
        {
            return Result.Fail(FailureReason.Cancelled, "Moins de deux groupes ont été sélectionnés.");
        }

        context.Set(VarCompareContextKeys.SelectedGroups, selected);

        return Result.Success();
    }

    /// <summary>
    /// Rapproche une sélection reprise de ce qui est lisible aujourd'hui, en écartant ce qui a disparu.
    /// </summary>
    /// <returns>
    /// Les groupes à comparer, ou <see langword="null"/> s'il ne s'agit pas d'une reprise ou s'il reste trop
    /// peu de groupes mémorisés pour que la comparaison ait un sens.
    /// </returns>
    private IReadOnlyList<VariableGroupSummary>? ResolveResumedSelection(
        ToolRunContext context,
        IReadOnlyList<VariableGroupSummary> available)
    {
        IReadOnlyList<PersistedGroupSelection>? remembered =
            context.Get<IReadOnlyList<PersistedGroupSelection>>(VarCompareContextKeys.ResumedSelection);

        if (remembered is null || remembered.Count == 0)
        {
            return null;
        }

        // Consommée une seule fois : une relance ultérieure doit redemander plutôt que de réutiliser en
        // silence une ancienne sélection.
        context.Set(VarCompareContextKeys.ResumedSelection, Array.Empty<PersistedGroupSelection>());

        // Le rapprochement est délégué, parce qu'écarter un groupe doit se dire et que dire est l'affaire de
        // la présentation.
        IReadOnlyList<VariableGroupSummary> kept = _reconciler.Reconcile(remembered, available);

        return kept.Count >= VariableComparison.MinimumGroups ? kept : null;
    }
}
