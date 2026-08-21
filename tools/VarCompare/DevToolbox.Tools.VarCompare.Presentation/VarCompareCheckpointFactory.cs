using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Core.Steps;

namespace DevToolbox.Tools.VarCompare.Presentation;

/// <summary>
/// Construit ce qui survit au processus pour une exécution varcompare interrompue.
/// </summary>
/// <remarks>
/// Noter ce que cette fabrique lit dans le contexte et ce qu'elle ignore : le projet, les groupes choisis
/// par id et par nom, et les noms des étapes achevées. Elle ne touche jamais
/// <see cref="VarCompareContextKeys.Snapshots"/>, où résident les valeurs de variables. C'est là tout le
/// mécanisme qui garantit qu'aucune valeur ne survit à la session.
/// </remarks>
public sealed class VarCompareCheckpointFactory : IRunCheckpointFactory
{
    private readonly ServerTarget _target;
    private readonly IClock _clock;

    /// <summary>Crée la fabrique.</summary>
    /// <param name="target">Fournit la collection consignée avec l'exécution.</param>
    /// <param name="clock">Fournit l'horodatage de dernière mise à jour qui pilote la péremption.</param>
    public VarCompareCheckpointFactory(ServerTarget target, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(clock);

        _target = target;
        _clock = clock;
    }

    /// <inheritdoc />
    public PersistedRun? TryCreate(ToolRunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ProjectIdentifier? project = context.Get<ProjectIdentifier>(VarCompareContextKeys.Project);

        if (project is null)
        {
            // Tant que le projet n'est pas connu, il n'y a rien qui vaille la peine d'être repris.
            return null;
        }

        IReadOnlyList<VariableGroupSummary> selected =
            context.Get<IReadOnlyList<VariableGroupSummary>>(VarCompareContextKeys.SelectedGroups) ?? [];

        return new PersistedRun(
            context.Run.RunId,
            context.Run.ToolId,
            _target.Collection,
            project.Name,
            [.. selected.Select(group => new PersistedGroupSelection(group.Id, group.Name))],
            context.Run.GetCompletedStepNames(),
            context.Run.StartedAt,
            _clock.UtcNow);
    }
}
