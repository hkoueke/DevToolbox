using System.Text.Json;
using DevToolbox.Application.Abstractions;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Ce qui survit au processus et, bien plus important, ce qui n'y survit pas.
/// </summary>
public sealed class RunCheckpointStoreTests
{
    private const string SecretValue = "this-value-must-never-reach-disk";

    private static readonly DateTimeOffset Now = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_serialized_checkpoint_contains_no_variable_value()
    {
        // Vérifié sur les octets écrits sur disque plutôt que sur le type : c'est ce contrôle qui
        // rattraperait une valeur glissée par un membre élargi.
        using TempPaths temp = new();
        FileRunCheckpointStore store = CreateStore(temp, Now);

        await store.SaveAsync(Checkpoint(), CancellationToken.None);

        string json = await File.ReadAllTextAsync(SingleRunFile(temp));

        json.Should().NotContain(SecretValue);
        json.Should().Contain("DefaultCollection");
        json.Should().Contain("app-prod");
    }

    [Fact]
    public async Task Only_identifiers_names_steps_and_timestamps_are_persisted()
    {
        using TempPaths temp = new();
        FileRunCheckpointStore store = CreateStore(temp, Now);

        await store.SaveAsync(Checkpoint(), CancellationToken.None);

        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(SingleRunFile(temp)));

        document.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "runId", "toolId", "collection", "project",
                "selectedGroups", "completedSteps", "startedAt", "lastUpdatedAt");
    }

    [Fact]
    public async Task A_saved_run_can_be_listed_for_resumption()
    {
        using TempPaths temp = new();
        FileRunCheckpointStore store = CreateStore(temp, Now);

        await store.SaveAsync(Checkpoint(), CancellationToken.None);

        IReadOnlyList<PersistedRun> resumable =
            await store.ListResumableAsync("varcompare", CancellationToken.None);

        resumable.Should().ContainSingle();
        resumable[0].Project.Should().Be("MyProject");
        resumable[0].SelectedGroups.Should().HaveCount(2);
    }

    [Fact]
    public async Task Runs_belonging_to_another_tool_are_not_offered()
    {
        using TempPaths temp = new();
        FileRunCheckpointStore store = CreateStore(temp, Now);

        await store.SaveAsync(Checkpoint(), CancellationToken.None);

        IReadOnlyList<PersistedRun> resumable =
            await store.ListResumableAsync("some-other-tool", CancellationToken.None);

        resumable.Should().BeEmpty();
    }

    [Fact]
    public async Task A_run_older_than_the_retention_window_is_swept()
    {
        // Une exécution mémorisée se périme au bout de sept jours.
        using TempPaths temp = new();

        FileRunCheckpointStore writer = CreateStore(temp, Now.AddDays(-8));
        await writer.SaveAsync(Checkpoint() with { LastUpdatedAt = Now.AddDays(-8) },
            CancellationToken.None);

        FileRunCheckpointStore sweeper = CreateStore(temp, Now);
        int removed = await sweeper.SweepExpiredAsync(TimeSpan.FromDays(7), CancellationToken.None);

        removed.Should().Be(1);
        (await sweeper.ListResumableAsync("varcompare", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_run_inside_the_retention_window_survives_the_sweep()
    {
        using TempPaths temp = new();
        FileRunCheckpointStore store = CreateStore(temp, Now);

        await store.SaveAsync(Checkpoint() with { LastUpdatedAt = Now.AddDays(-2) },
            CancellationToken.None);

        int removed = await store.SweepExpiredAsync(TimeSpan.FromDays(7), CancellationToken.None);

        removed.Should().Be(0);
        (await store.ListResumableAsync("varcompare", CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_discarded_run_is_gone()
    {
        using TempPaths temp = new();
        FileRunCheckpointStore store = CreateStore(temp, Now);

        PersistedRun run = Checkpoint();
        await store.SaveAsync(run, CancellationToken.None);
        await store.DiscardAsync(run.RunId, CancellationToken.None);

        (await store.ListResumableAsync("varcompare", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_corrupt_checkpoint_is_swept_rather_than_offered()
    {
        // Un point de reprise illisible ne peut pas être repris : le garder ne laisserait qu'une entrée qui
        // échoue au moment où le développeur la choisit.
        using TempPaths temp = new();
        FileRunCheckpointStore store = CreateStore(temp, Now);

        Directory.CreateDirectory(temp.Paths.RunsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(temp.Paths.RunsDirectory, "broken.json"), "{ not valid json");

        (await store.ListResumableAsync("varcompare", CancellationToken.None)).Should().BeEmpty();
        (await store.SweepExpiredAsync(TimeSpan.FromDays(7), CancellationToken.None)).Should().Be(1);
    }

    private static string SingleRunFile(TempPaths temp) =>
        Directory.EnumerateFiles(temp.Paths.RunsDirectory, "*.json").Single();

    private static FileRunCheckpointStore CreateStore(TempPaths temp, DateTimeOffset now)
    {
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);

        return new FileRunCheckpointStore(
            temp.Paths, clock, NullLogger<FileRunCheckpointStore>.Instance);
    }

    private static PersistedRun Checkpoint() => new(
        Guid.NewGuid(),
        "varcompare",
        "DefaultCollection",
        "MyProject",
        [new PersistedGroupSelection(1, "app-dev"), new PersistedGroupSelection(2, "app-prod")],
        ["IdentifyProject", "ListGroups"],
        Now.AddMinutes(-5),
        Now);

    /// <summary>
    /// Redirige les dossiers de l'application vers un répertoire temporaire, le temps d'un test.
    /// </summary>
    private sealed class TempPaths : IDisposable
    {
        private readonly string _root;

        internal TempPaths()
        {
            _root = Path.Combine(Path.GetTempPath(), "devtoolbox-tests", Guid.NewGuid().ToString("N"));

            Paths = new AppPaths(_root, _root);
            Paths.EnsureCreated();
        }

        internal AppPaths Paths { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
