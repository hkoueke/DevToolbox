using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Targets;

namespace DevToolbox.Tools.VarCompare.Core.Steps;

/// <summary>
/// Étape 1 : établir sur quelle collection et quel projet travailler.
/// </summary>
/// <remarks>
/// Un dépôt détecté est présenté pour confirmation, jamais appliqué en silence, et le développeur peut
/// toujours choisir un autre projet à la place.
/// </remarks>
public sealed class IdentifyProjectStep : IToolStep
{
    /// <summary>Le nom sous lequel cette étape apparaît dans les journaux et l'invite de reprise.</summary>
    public const string StepName = "IdentifyProject";

    private readonly IVariableGroupGateway _gateway;
    private readonly IWorkingFolderInspector _inspector;
    private readonly IProjectChooser _chooser;

    /// <summary>Crée l'étape.</summary>
    /// <param name="gateway">Liste les projets accessibles.</param>
    /// <param name="inspector">Détecte le projet à partir du dossier de travail.</param>
    /// <param name="chooser">Demande au développeur de confirmer ou de choisir.</param>
    public IdentifyProjectStep(
        IVariableGroupGateway gateway,
        IWorkingFolderInspector inspector,
        IProjectChooser chooser)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(chooser);

        _gateway = gateway;
        _inspector = inspector;
        _chooser = chooser;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RepositoryOrigin? detected = _inspector
            .DetectOrigin(Directory.GetCurrentDirectory())
            .Value;

        Result<IReadOnlyList<ProjectIdentifier>> projects =
            await _gateway.ListProjectsAsync(cancellationToken).ConfigureAwait(false);

        if (projects.IsFailure)
        {
            return Result.Fail(projects.Failure!);
        }

        ProjectIdentifier? chosen = await _chooser
            .ChooseAsync(detected, projects.Value, cancellationToken)
            .ConfigureAwait(false);

        if (chosen is null)
        {
            return Result.Fail(FailureReason.Cancelled, "Aucun projet n'a été choisi.");
        }

        context.Set(VarCompareContextKeys.Project, chosen);

        return Result.Success();
    }
}
