using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Application;

/// <summary>
/// Le chemin nominal du moteur d'étapes.
/// </summary>
public sealed class SequentialStepRunnerTests
{
    private static readonly string[] StepNames = ["First", "Second", "Third"];

    [Fact]
    public async Task Steps_execute_in_declared_order()
    {
        List<string> executed = [];

        SequentialStepRunner runner = CreateRunner(out _, out _);
        ToolRunContext context = CreateContext();

        IReadOnlyList<IToolStep> steps =
        [
            RecordingStep("First", executed),
            RecordingStep("Second", executed),
            RecordingStep("Third", executed),
        ];

        await runner.RunAsync(context, steps, CancellationToken.None);

        executed.Should().Equal("First", "Second", "Third");
    }

    [Fact]
    public async Task Successful_run_ends_as_Success_with_a_recorded_duration()
    {
        SequentialStepRunner runner = CreateRunner(out _, out _);
        ToolRunContext context = CreateContext();

        ToolRun run = await runner.RunAsync(context, AllSucceeding(), CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Success);
        run.EndedAt.Should().NotBeNull();
        run.Duration.Should().NotBeNull();
        run.Steps.Should().OnlyContain(step => step.Status == StepStatus.Completed);
    }

    [Fact]
    public async Task Each_successful_step_checkpoints_exactly_once()
    {
        SequentialStepRunner runner = CreateRunner(out IRunCheckpointStore store, out _);
        ToolRunContext context = CreateContext();

        await runner.RunAsync(context, AllSucceeding(), CancellationToken.None);

        await store.Received(StepNames.Length).SaveAsync(
            Arg.Any<PersistedRun>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_step_already_completed_is_not_executed_again()
    {
        // C'est le mécanisme qui fait que poursuivre ne refait jamais le travail déjà acquis.
        List<string> executed = [];

        SequentialStepRunner runner = CreateRunner(out _, out _);
        ToolRunContext context = CreateContext();

        StepState first = context.Run.FindStep("First")!;
        first.Start(DateTimeOffset.UnixEpoch);
        first.Complete(DateTimeOffset.UnixEpoch);

        IReadOnlyList<IToolStep> steps =
        [
            RecordingStep("First", executed),
            RecordingStep("Second", executed),
            RecordingStep("Third", executed),
        ];

        await runner.RunAsync(context, steps, CancellationToken.None);

        executed.Should().Equal("Second", "Third");
    }

    private static SequentialStepRunner CreateRunner(
        out IRunCheckpointStore store,
        out IRunCheckpointFactory factory) =>
        CreateRunner(out store, out factory, out _);

    private static SequentialStepRunner CreateRunner(
        out IRunCheckpointStore store,
        out IRunCheckpointFactory factory,
        out IRecoveryPrompt recovery)
    {
        recovery = Substitute.For<IRecoveryPrompt>();
        recovery.AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>())
            .Returns(RecoveryChoice.Abort);

        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_ => DateTimeOffset.UnixEpoch.AddSeconds(Environment.TickCount64 % 1000));

        store = Substitute.For<IRunCheckpointStore>();

        factory = Substitute.For<IRunCheckpointFactory>();
        factory.TryCreate(Arg.Any<ToolRunContext>()).Returns(new PersistedRun(
            Guid.NewGuid(),
            "test",
            "DefaultCollection",
            "Project",
            [],
            [],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch));

        return new SequentialStepRunner(
            clock, store, factory, recovery, NullLogger<SequentialStepRunner>.Instance);
    }

    private static ToolRunContext CreateContext() =>
        new(new ToolRun(Guid.NewGuid(), "test", DateTimeOffset.UnixEpoch, StepNames));

    private static IReadOnlyList<IToolStep> AllSucceeding()
    {
        List<string> ignored = [];
        return [.. StepNames.Select(name => RecordingStep(name, ignored))];
    }

    private static IToolStep RecordingStep(string name, List<string> executed)
    {
        IToolStep step = Substitute.For<IToolStep>();
        step.Name.Returns(name);
        step.IsIdempotent.Returns(true);

        step.ExecuteAsync(Arg.Any<ToolRunContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                executed.Add(name);
                return Task.FromResult(Result.Success());
            });

        return step;
    }
}
