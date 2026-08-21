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
/// Les quatre possibilités de reprise après une étape en échec : relance, poursuite, reprise complète et
/// abandon.
/// </summary>
public sealed class RecoveryTests
{
    private static readonly string[] StepNames = ["First", "Second", "Third"];

    [Fact]
    public async Task A_failed_step_asks_the_developer_what_to_do()
    {
        IRecoveryPrompt prompt = PromptAnswering(RecoveryChoice.Abort);
        SequentialStepRunner runner = CreateRunner(prompt, out _);
        ToolRunContext context = CreateContext();

        await runner.RunAsync(context, [Succeeds("First"), Fails("Second"), Succeeds("Third")],
            CancellationToken.None);

        await prompt.Received(1).AskAsync(
            Arg.Is<StepState>(step => step.Name == "Second"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retry_runs_the_failed_step_again_and_the_run_completes_when_it_succeeds()
    {
        int attempts = 0;

        IToolStep flaky = Substitute.For<IToolStep>();
        flaky.Name.Returns("Second");
        flaky.IsIdempotent.Returns(true);
        flaky.ExecuteAsync(Arg.Any<ToolRunContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return Task.FromResult(
                    attempts == 1
                        ? Result.Fail(FailureReason.ServerUnreachable, "the network blinked")
                        : Result.Success());
            });

        SequentialStepRunner runner = CreateRunner(PromptAnswering(RecoveryChoice.RetryStep), out _);
        ToolRunContext context = CreateContext();

        ToolRun run = await runner.RunAsync(
            context, [Succeeds("First"), flaky, Succeeds("Third")], CancellationToken.None);

        attempts.Should().Be(2);
        run.Outcome.Should().Be(RunOutcome.Success);
    }

    [Fact]
    public async Task Continue_carries_on_past_the_failed_step_without_repeating_completed_work()
    {
        // Le travail déjà fait est réutilisé, pas refait.
        List<string> executed = [];

        SequentialStepRunner runner =
            CreateRunner(PromptAnswering(RecoveryChoice.ContinueFromStep), out _);
        ToolRunContext context = CreateContext();

        ToolRun run = await runner.RunAsync(
            context,
            [Recording("First", executed), Fails("Second"), Recording("Third", executed)],
            CancellationToken.None);

        executed.Should().Equal("First", "Third");
        run.FindStep("First")!.Status.Should().Be(StepStatus.Completed);
        run.FindStep("Second")!.Status.Should().Be(StepStatus.Skipped);
        run.FindStep("Third")!.Status.Should().Be(StepStatus.Completed);
        run.Outcome.Should().Be(RunOutcome.Success);
    }

    [Fact]
    public async Task Restart_returns_every_step_to_pending_and_runs_them_all_again()
    {
        // La reprise complète doit repartir réellement de zéro.
        List<string> executed = [];
        bool failedOnce = false;

        IToolStep failsFirstTimeOnly = Substitute.For<IToolStep>();
        failsFirstTimeOnly.Name.Returns("Second");
        failsFirstTimeOnly.IsIdempotent.Returns(true);
        failsFirstTimeOnly.ExecuteAsync(Arg.Any<ToolRunContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                executed.Add("Second");

                if (failedOnce)
                {
                    return Task.FromResult(Result.Success());
                }

                failedOnce = true;
                return Task.FromResult(Result.Fail(FailureReason.ServiceUnavailable, "unavailable"));
            });

        SequentialStepRunner runner = CreateRunner(PromptAnswering(RecoveryChoice.RestartAll), out _);
        ToolRunContext context = CreateContext();

        ToolRun run = await runner.RunAsync(
            context,
            [Recording("First", executed), failsFirstTimeOnly, Recording("Third", executed)],
            CancellationToken.None);

        // « First » s'exécute deux fois, parce qu'une reprise complète recommence vraiment.
        executed.Should().Equal("First", "Second", "First", "Second", "Third");
        run.Outcome.Should().Be(RunOutcome.Success);
    }

    [Fact]
    public async Task Restart_discards_everything_the_earlier_attempt_held()
    {
        // Rien ne doit survivre à une reprise complète, sans quoi la nouvelle exécution partirait d'un état
        // périmé.
        SequentialStepRunner runner = CreateRunner(PromptAnswering(RecoveryChoice.Abort), out _);
        ToolRunContext context = CreateContext();

        IToolStep stores = Substitute.For<IToolStep>();
        stores.Name.Returns("First");
        stores.IsIdempotent.Returns(true);
        stores.ExecuteAsync(Arg.Any<ToolRunContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<ToolRunContext>().Set("held", "snapshot-data");
                return Task.FromResult(Result.Success());
            });

        await runner.RunAsync(context, [stores], CancellationToken.None);
        context.Has("held").Should().BeTrue();

        context.Run.ResetAllSteps();
        context.Clear();

        context.Has("held").Should().BeFalse();
        context.Run.Steps.Should().OnlyContain(step => step.Status == StepStatus.Pending);
    }

    [Fact]
    public async Task Abort_ends_the_run_as_abandoned_and_stops_immediately()
    {
        List<string> executed = [];

        SequentialStepRunner runner = CreateRunner(PromptAnswering(RecoveryChoice.Abort), out _);
        ToolRunContext context = CreateContext();

        ToolRun run = await runner.RunAsync(
            context,
            [Recording("First", executed), Fails("Second"), Recording("Third", executed)],
            CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Abandoned);
        executed.Should().Equal("First");
        run.FindStep("Third")!.Status.Should().Be(StepStatus.Pending);
    }

    [Fact]
    public async Task Cancellation_ends_the_run_without_asking_for_a_recovery_choice()
    {
        // Ctrl+C n'est pas un échec dont on se remet, c'est le développeur qui s'en va.
        IRecoveryPrompt prompt = PromptAnswering(RecoveryChoice.RetryStep);
        SequentialStepRunner runner = CreateRunner(prompt, out _);
        ToolRunContext context = CreateContext();

        IToolStep cancels = Substitute.For<IToolStep>();
        cancels.Name.Returns("First");
        cancels.IsIdempotent.Returns(true);
        cancels.ExecuteAsync(Arg.Any<ToolRunContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail(FailureReason.Cancelled, "cancelled")));

        ToolRun run = await runner.RunAsync(context, [cancels], CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Cancelled);
        await prompt.DidNotReceive().AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>());
    }

    private static IRecoveryPrompt PromptAnswering(RecoveryChoice choice)
    {
        IRecoveryPrompt prompt = Substitute.For<IRecoveryPrompt>();
        prompt.AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>()).Returns(choice);

        return prompt;
    }

    private static SequentialStepRunner CreateRunner(
        IRecoveryPrompt prompt,
        out IRunCheckpointStore store)
    {
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_ => DateTimeOffset.UnixEpoch.AddTicks(Environment.TickCount64));

        store = Substitute.For<IRunCheckpointStore>();

        IRunCheckpointFactory factory = Substitute.For<IRunCheckpointFactory>();
        factory.TryCreate(Arg.Any<ToolRunContext>()).Returns((PersistedRun?)null);

        return new SequentialStepRunner(
            clock, store, factory, prompt, NullLogger<SequentialStepRunner>.Instance);
    }

    private static ToolRunContext CreateContext() =>
        new(new ToolRun(Guid.NewGuid(), "test", DateTimeOffset.UnixEpoch, StepNames));

    private static IToolStep Succeeds(string name) => Recording(name, []);

    private static IToolStep Recording(string name, List<string> executed)
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

    private static IToolStep Fails(string name)
    {
        IToolStep step = Substitute.For<IToolStep>();
        step.Name.Returns(name);
        step.IsIdempotent.Returns(true);
        step.ExecuteAsync(Arg.Any<ToolRunContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail(FailureReason.ServerUnreachable, "the server is down")));

        return step;
    }
}
