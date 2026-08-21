using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Steps;

/// <summary>Étape 5 : construire la comparaison à partir des clichés obtenus.</summary>
public sealed class BuildComparisonStep : IToolStep
{
    /// <summary>Le nom sous lequel cette étape apparaît dans les journaux et l'invite de reprise.</summary>
    public const string StepName = "BuildComparison";

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<VariableGroupSummary>? selected =
            context.Get<IReadOnlyList<VariableGroupSummary>>(VarCompareContextKeys.SelectedGroups);

        Dictionary<int, VariableGroupSnapshot>? snapshots =
            context.Get<Dictionary<int, VariableGroupSnapshot>>(VarCompareContextKeys.Snapshots);

        if (selected is null || snapshots is null)
        {
            return Task.FromResult(
                Result.Fail(FailureReason.InvalidConfiguration, "Aucun groupe n'a encore été lu."));
        }

        // L'ordre des colonnes suit l'ordre de sélection, et non celui dans lequel les réponses sont arrivées.
        List<VariableGroupSnapshot> ordered = [];

        foreach (VariableGroupSummary group in selected)
        {
            if (snapshots.TryGetValue(group.Id, out VariableGroupSnapshot? snapshot))
            {
                ordered.Add(snapshot);
            }
        }

        if (ordered.Count < VariableComparison.MinimumGroups)
        {
            return Task.FromResult(
                Result.Fail(
                    FailureReason.GroupNotFound,
                    "Moins de deux groupes ont pu être lus : il n'y a rien à comparer."));
        }

        context.Set(VarCompareContextKeys.Comparison, ComparisonBuilder.Build(ordered));

        return Task.FromResult(Result.Success());
    }
}
