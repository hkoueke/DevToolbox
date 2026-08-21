using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Steps;

/// <summary>
/// Étape 4 : lire les variables de chaque groupe retenu, avec un parallélisme borné.
/// </summary>
/// <remarks>
/// <para>
/// Les groupes déjà obtenus au cours de cette session ne sont pas relus : c'est ce qui fait que
/// <em>poursuivre</em> après un échec reprend vraiment au lieu de tout recommencer.
/// </para>
/// <para>
/// La concurrence est bornée et configurable, jamais une rafale illimitée lancée sur un réseau d'entreprise.
/// </para>
/// </remarks>
public sealed class RetrieveGroupsStep : IToolStep
{
    /// <summary>Le nom sous lequel cette étape apparaît dans les journaux et l'invite de reprise.</summary>
    public const string StepName = "RetrieveGroups";

    private readonly IVariableGroupGateway _gateway;
    private readonly IRetrievalProgress _progress;
    private readonly IClock _clock;
    private readonly int _maxDegreeOfParallelism;

    /// <summary>Crée l'étape.</summary>
    /// <param name="gateway">Lit chaque groupe.</param>
    /// <param name="progress">Rend compte groupe par groupe, pour que les reprises se voient.</param>
    /// <param name="clock">Fournit l'horodatage apposé sur un cliché de remplacement dégradé.</param>
    /// <param name="maxDegreeOfParallelism">Combien de groupes peuvent être lus simultanément.</param>
    public RetrieveGroupsStep(
        IVariableGroupGateway gateway,
        IRetrievalProgress progress,
        IClock clock,
        int maxDegreeOfParallelism)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);

        _gateway = gateway;
        _progress = progress;
        _clock = clock;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
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
        IReadOnlyList<VariableGroupSummary>? selected =
            context.Get<IReadOnlyList<VariableGroupSummary>>(VarCompareContextKeys.SelectedGroups);

        if (project is null || selected is null)
        {
            return Result.Fail(
                FailureReason.InvalidConfiguration, "Aucun groupe n'a encore été sélectionné.");
        }

        Dictionary<int, VariableGroupSnapshot> snapshots =
            context.Get<Dictionary<int, VariableGroupSnapshot>>(VarCompareContextKeys.Snapshots) ?? [];

        List<VariableGroupSummary> outstanding =
            [.. selected.Where(group => !snapshots.ContainsKey(group.Id))];

        _progress.Begin(selected.Count, selected.Count - outstanding.Count);

        Failure? firstFailure = await RetrieveAllAsync(
            project, outstanding, snapshots, cancellationToken).ConfigureAwait(false);

        // Ce qui a abouti est conservé, pour qu'une poursuite après échec le réutilise.
        context.Set(VarCompareContextKeys.Snapshots, snapshots);

        _progress.Complete();

        return firstFailure is null ? Result.Success() : Result.Fail(firstFailure);
    }

    private async Task<Failure?> RetrieveAllAsync(
        ProjectIdentifier project,
        IReadOnlyList<VariableGroupSummary> outstanding,
        Dictionary<int, VariableGroupSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        Lock gate = new();
        Failure? firstFailure = null;

        await Parallel.ForEachAsync(
            outstanding,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken,
            },
            async (group, token) =>
            {
                _progress.Retrieving(group.Name);

                Result<VariableGroupSnapshot> snapshot = await _gateway
                    .GetGroupAsync(project, group.Id, token)
                    .ConfigureAwait(false);

                bool degraded = snapshot.IsFailure && IsGroupLevelFailure(snapshot.Failure!.Reason);

                lock (gate)
                {
                    if (snapshot.IsSuccess)
                    {
                        snapshots[group.Id] = snapshot.Value;
                    }
                    else if (degraded)
                    {
                        // Un groupe que ce développeur ne peut pas lire doit apparaître comme une colonne
                        // explicitement indéterminée, ni vide ni fatale pour l'exécution. Les groupes
                        // lisibles valent toujours la peine d'être comparés.
                        snapshots[group.Id] = DegradedSnapshot(group, _clock.UtcNow);
                    }
                    else
                    {
                        // Les échecs d'authentification, de connectivité et de service sont systémiques : tous
                        // les groupes restants échoueraient de la même façon. L'exécution s'arrête et le dit.
                        firstFailure ??= snapshot.Failure;
                    }
                }

                if (snapshot.IsSuccess)
                {
                    _progress.Retrieved(group.Name);
                }
                else
                {
                    _progress.Failed(group.Name, snapshot.Failure!.Message);
                }
            }).ConfigureAwait(false);

        return firstFailure;
    }

    /// <summary>
    /// Indique si un échec ne concerne que ce groupe, laissant le reste de la comparaison utile.
    /// </summary>
    /// <param name="reason">La raison de l'échec.</param>
    /// <returns><see langword="true"/> si le groupe peut être montré comme indéterminé au lieu d'échouer.</returns>
    private static bool IsGroupLevelFailure(FailureReason reason) =>
        reason is FailureReason.PermissionDenied or FailureReason.GroupNotFound;

    /// <summary>
    /// Un cliché de remplacement pour un groupe retenu mais illisible, afin que sa colonne annonce un état
    /// inconnu au lieu de paraître vide.
    /// </summary>
    private static VariableGroupSnapshot DegradedSnapshot(VariableGroupSummary group, DateTimeOffset at) =>
        new(group, [], at, modifiedOn: null, isDegraded: true);
}
