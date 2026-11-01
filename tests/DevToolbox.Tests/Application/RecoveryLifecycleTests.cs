using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Infrastructure.Storage;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Presentation.Rendering;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Application;

public sealed class RecoveryLifecycleTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "recovery-lifecycle-" + Guid.NewGuid().ToString("N"));
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly FileRunCheckpointStore _store;

    public RecoveryLifecycleTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        _store = new FileRunCheckpointStore(new AppPaths(_directory, _directory), _clock,
            NullLogger<FileRunCheckpointStore>.Instance);
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task Successful_completion_removes_only_its_own_session_without_touching_results()
    {
        string report = Path.Combine(_directory, "report.txt");
        await File.WriteAllTextAsync(report, "keep this report");
        await _store.SaveAsync(Saved(Guid.NewGuid(), "another-tool"), CancellationToken.None);
        using TestConsole console = Ready(0);
        ToolRunContext context = Context();
        ToolRun run = await Runner(console).RunAsync(context,
            [Step("Choose", _ => Result.Success()), Step("Read", _ => Result.Success())], CancellationToken.None);
        run.Outcome.Should().Be(RunOutcome.Success);
        (await _store.ListResumableAsync("test", CancellationToken.None)).Should().BeEmpty();
        (await _store.ListResumableAsync("another-tool", CancellationToken.None)).Should().ContainSingle();
        (await File.ReadAllTextAsync(report)).Should().Be("keep this report");
    }

    [Fact]
    public async Task Restart_clears_memory_and_disk_before_asking_again_even_if_new_selection_is_cancelled()
    {
        int choices = 0;
        using TestConsole console = Ready(2);
        ToolRunContext context = Context();
        IToolStep choose = AsyncStep("Choose", async current =>
        {
            choices++;
            if (choices == 1)
            {
                current.Set("old-choice", "old");
                return Result.Success();
            }

            current.Has("old-choice").Should().BeFalse();
            (await _store.ListResumableAsync("test", CancellationToken.None)).Should().BeEmpty();
            return Result.Fail(FailureReason.Cancelled, "new selection cancelled");
        });
        ToolRun run = await Runner(console).RunAsync(context,
            [choose, Step("Read", _ => Failure())], CancellationToken.None);
        choices.Should().Be(2);
        run.Outcome.Should().Be(RunOutcome.Cancelled);
        (await _store.ListResumableAsync("test", CancellationToken.None)).Should().BeEmpty();
        console.Output.Should().Contain("redemander les choix").And.Contain("déjà écrits sont conservés");
    }

    [Fact]
    public async Task Retry_reexecutes_only_the_failed_step_with_the_previous_choices()
    {
        int selected = 0;
        int reads = 0;
        using TestConsole console = Ready(0);
        ToolRunContext context = Context();
        ToolRun run = await Runner(console).RunAsync(context,
        [
            Step("Choose", current => { selected++; current.Set("choice", "kept"); return Result.Success(); }),
            Step("Read", current =>
            {
                current.Get<string>("choice").Should().Be("kept");
                return ++reads == 1 ? Failure() : Result.Success();
            }),
        ], CancellationToken.None);
        selected.Should().Be(1);
        reads.Should().Be(2);
        run.Outcome.Should().Be(RunOutcome.Success);
        (await _store.ListResumableAsync("test", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Skipping_after_failure_keeps_the_session_and_is_not_presented_as_a_normal_skip()
    {
        using TestConsole console = Ready(1);
        ToolRunContext context = Context();
        ToolRun run = await Runner(console).RunAsync(context,
            [Step("Choose", _ => Result.Success()), Step("Read", _ => Failure())], CancellationToken.None);
        run.Outcome.Should().Be(RunOutcome.Success);
        run.Steps[1].Status.Should().Be(StepStatus.Skipped);
        (await _store.ListResumableAsync("test", CancellationToken.None)).Should().ContainSingle();
        new SelectionSummaryRenderer(console).Render(new RunSelection(RunMode.Analyse, "input.yaml", "hash", _directory), run);
        console.Output.Should().Contain("étape ignorée après erreur").And.Contain("Read").And.Contain("network unavailable");
    }

    [Fact]
    public async Task Stop_keeps_last_saved_choices_for_the_next_opening()
    {
        using TestConsole console = Ready(3);
        ToolRunContext context = Context();
        ToolRun run = await Runner(console).RunAsync(context,
            [Step("Choose", _ => Result.Success()), Step("Read", _ => Failure())], CancellationToken.None);
        run.Outcome.Should().Be(RunOutcome.Abandoned);
        var saved = await _store.ListResumableAsync("test", CancellationToken.None);
        saved.Should().ContainSingle().Which.CompletedSteps.Should().Equal("Choose");
        console.Output.Should().Contain("dernier enregistrement").And.Contain("s’il existe");
    }

    [Fact]
    public async Task A_resumed_session_is_retired_even_when_no_new_checkpoint_was_created()
    {
        Guid old = Guid.NewGuid();
        await _store.SaveAsync(Saved(old, "test"), CancellationToken.None);
        ToolRunContext context = Context();
        context.Set(CheckpointContextKeys.SourceRunId, old);
        using TestConsole console = Ready(0);
        var factory = Substitute.For<IRunCheckpointFactory>();
        factory.ToolId.Returns("test");
        factory.TryCreate(Arg.Any<ToolRunContext>()).Returns((PersistedRun?)null);
        var runner = new SequentialStepRunner(_clock, _store, [factory], new SpectreRecoveryPrompt(console),
            NullLogger<SequentialStepRunner>.Instance);
        await runner.RunAsync(context, [Step("Choose", _ => Result.Success()), Step("Read", _ => Result.Success())], CancellationToken.None);
        (await _store.ListResumableAsync("test", CancellationToken.None)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_unavailable_checkpoint_delete_is_explained_and_does_not_fake_a_restart(bool restart)
    {
        using TestConsole console = Ready(restart ? 2 : 0);
        IRunCheckpointStore unavailable = Substitute.For<IRunCheckpointStore>();
        unavailable.DiscardAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new IOException("storage unavailable")));
        int choices = 0;
        ToolRunContext context = Context();
        ToolRun run = await Runner(console, unavailable).RunAsync(context,
        [
            Step("Choose", current => { choices++; current.Set("choice", "kept"); return Result.Success(); }),
            Step("Read", _ => restart ? Failure() : Result.Success()),
        ], CancellationToken.None);
        run.Outcome.Should().Be(restart ? RunOutcome.Abandoned : RunOutcome.Success);
        choices.Should().Be(1);
        context.Get<string>("choice").Should().Be("kept");
        ResumePrompt.RenderCleanupWarning(console, context);
        console.Output.Should().Contain("n’a pas pu être oubliée").And.Contain("anciens choix peuvent encore être proposés");
    }

    private SequentialStepRunner Runner(TestConsole console, IRunCheckpointStore? store = null)
    {
        var factory = Substitute.For<IRunCheckpointFactory>();
        factory.ToolId.Returns("test");
        factory.TryCreate(Arg.Any<ToolRunContext>()).Returns(call =>
        {
            ToolRun run = call.Arg<ToolRunContext>().Run;
            return Saved(run.RunId, run.ToolId) with { CompletedSteps = run.GetCompletedStepNames() };
        });
        return new SequentialStepRunner(_clock, store ?? _store, [factory], new SpectreRecoveryPrompt(console),
            NullLogger<SequentialStepRunner>.Instance);
    }

    private static PersistedRun Saved(Guid id, string tool) =>
        new(id, tool, [new PersistedSelectionEntry("choice", "1", "first")], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static ToolRunContext Context() =>
        new(new ToolRun(Guid.NewGuid(), "test", DateTimeOffset.UnixEpoch, ["Choose", "Read"]));

    private static IToolStep Step(string name, Func<ToolRunContext, Result> action) =>
        AsyncStep(name, current => Task.FromResult(action(current)));

    private static IToolStep AsyncStep(string name, Func<ToolRunContext, Task<Result>> action)
    {
        var step = Substitute.For<IToolStep>();
        step.Name.Returns(name);
        step.IsIdempotent.Returns(true);
        step.ExecuteAsync(Arg.Any<ToolRunContext>(), Arg.Any<CancellationToken>())
            .Returns(call => action(call.Arg<ToolRunContext>()));
        return step;
    }

    private static Result Failure() => Result.Fail(FailureReason.ServerUnreachable, "network unavailable");

    private static TestConsole Ready(int down)
    {
        var console = new TestConsole().Interactive();
        console.Profile.Width = 240;
        for (int i = 0; i < down; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
        return console;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
