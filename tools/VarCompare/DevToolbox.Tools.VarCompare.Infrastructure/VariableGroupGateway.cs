using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.AzureDevOps;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Infrastructure.Wire;
using Microsoft.Extensions.Options;

namespace DevToolbox.Tools.VarCompare.Infrastructure;

/// <summary>
/// Lit les projets et les groupes de variables via le client Azure DevOps partagé.
/// </summary>
/// <remarks>
/// Chaque appel ci-dessous est un GET. Aucun autre verbe ne figure dans ce fichier, et la compilation échoue
/// si l'un d'eux venait à y apparaître.
/// </remarks>
public sealed class VariableGroupGateway : IVariableGroupGateway
{
    private readonly AzureDevOpsApiReader _reader;
    private readonly AzureDevOpsServerOptions _options;
    private readonly IClock _clock;

    /// <summary>Crée la passerelle.</summary>
    /// <param name="reader">Le lecteur d'API partagé, en lecture seule.</param>
    /// <param name="options">Les options serveur validées.</param>
    /// <param name="clock">Fournit l'horodatage de lecture apposé sur chaque cliché.</param>
    public VariableGroupGateway(
        AzureDevOpsApiReader reader,
        IOptions<AzureDevOpsServerOptions> options,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _reader = reader;
        _options = options.Value;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ProjectIdentifier>>> ListProjectsAsync(
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ProjectDto>> projects = await _reader
            .GetAllPagesAsync<ProjectDto>(
                token => VariableGroupRoutes.ListProjects(
                    _options.Collection, _options.ApiVersion, token),
                cancellationToken)
            .ConfigureAwait(false);

        if (projects.IsFailure)
        {
            return Result.Fail<IReadOnlyList<ProjectIdentifier>>(projects.Failure!);
        }

        // Un projet dont le nom, tel que le serveur l'orthographie, ne peut pas figurer dans une URL est
        // écarté plutôt que laissé casser toute la liste.
        IEnumerable<ProjectIdentifier> identifiers = projects.Value
            .Where(dto => ProjectIdentifier.IsSafeSegment(dto.Name))
            .Select(dto => new ProjectIdentifier(dto.Name, dto.Id))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase);

        return Result.Success<IReadOnlyList<ProjectIdentifier>>([.. identifiers]);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<VariableGroupSummary>>> ListGroupsAsync(
        ProjectIdentifier project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        Result<IReadOnlyList<VariableGroupDto>> groups = await _reader
            .GetAllPagesAsync<VariableGroupDto>(
                token => VariableGroupRoutes.ListGroups(
                    _options.Collection, project, _options.ApiVersion, token),
                cancellationToken)
            .ConfigureAwait(false);

        if (groups.IsFailure)
        {
            return Result.Fail<IReadOnlyList<VariableGroupSummary>>(groups.Failure!);
        }

        return Result.Success<IReadOnlyList<VariableGroupSummary>>(
        [
            .. groups.Value
                .Select(VariableGroupMapper.ToSummary)
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase),
        ]);
    }

    /// <inheritdoc />
    public async Task<Result<VariableGroupSnapshot>> GetGroupAsync(
        ProjectIdentifier project,
        int groupId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        Result<VariableGroupDto> group = await _reader
            .GetAsync<VariableGroupDto>(
                VariableGroupRoutes.GetGroup(_options.Collection, project, groupId, _options.ApiVersion),
                cancellationToken)
            .ConfigureAwait(false);

        return group.IsFailure
            ? Result.Fail<VariableGroupSnapshot>(group.Failure!)
            : Result.Success(VariableGroupMapper.ToSnapshot(group.Value, _clock.UtcNow));
    }
}
