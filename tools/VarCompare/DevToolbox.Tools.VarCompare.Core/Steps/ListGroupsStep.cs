using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Steps;

/// <summary>Étape 2 : lister les groupes de variables lisibles par le développeur dans le projet retenu.</summary>
public sealed class ListGroupsStep : IToolStep
{
    /// <summary>Le nom sous lequel cette étape apparaît dans les journaux et l'invite de reprise.</summary>
    public const string StepName = "ListGroups";

    private readonly IVariableGroupGateway _gateway;

    /// <summary>Crée l'étape.</summary>
    /// <param name="gateway">Liste les groupes.</param>
    public ListGroupsStep(IVariableGroupGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        _gateway = gateway;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        ProjectIdentifier? project = context.Get<ProjectIdentifier>(VarCompareContextKeys.Project);

        if (project is null)
        {
            return Result.Fail(FailureReason.InvalidConfiguration, "No project has been chosen yet.");
        }

        Result<IReadOnlyList<VariableGroupSummary>> groups =
            await _gateway.ListGroupsAsync(project, cancellationToken).ConfigureAwait(false);

        if (groups.IsFailure)
        {
            return Result.Fail(groups.Failure!);
        }

        context.Set(VarCompareContextKeys.AvailableGroups, groups.Value);

        return Result.Success();
    }
}
