using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Presentation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta;

/// <summary>
/// Les quatre chemins qu'une exécution doit savoir emprunter : réussir, échouer, être relancée étape par
/// étape, et être reprise au démarrage suivant.
/// </summary>
public sealed class ApiDeltaRecoveryTests
{
    private const string ProductionPath = @"C:\travail\production.yaml";
    private const string ProductionHash = "3f9a7c21b0d4e8f1a2b3c4d5e6f7a8b9";

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_verified_selection_completes_the_step_and_is_checkpointed()
    {
        Harness harness = new();

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);
        harness.Context.Get<RunSelection>(ApiDeltaContextKeys.Selection).Should().NotBeNull();

        // L'empreinte vient du fichier, non de l'invite : c'est l'étape qui établit ce qui a été lu.
        harness.Context.Get<RunSelection>(ApiDeltaContextKeys.Selection)!.ProductionHash
            .Should().Be(ProductionHash);

        await harness.Store.Received().SaveAsync(
            Arg.Is<PersistedRun>(checkpoint => checkpoint.ToolId == "apidelta"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unreadable_file_fails_the_step_and_the_developer_is_asked_what_to_do()
    {
        Harness harness = new();
        harness.ProbeFails("Ce fichier n'existe pas : " + ProductionPath);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Abandoned);
        run.FindStep(ApiDeltaStepNames.Select)!.Status.Should().Be(StepStatus.Failed);
        run.FindStep(ApiDeltaStepNames.Select)!.FailureReason.Should().Contain("n'existe pas");

        await harness.Recovery.Received().AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_retried_step_succeeds_the_second_time()
    {
        Harness harness = new();
        harness.ProbeFailsOnceThenSucceeds();
        harness.RecoveryChooses(RecoveryChoice.RetryStep);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);
        run.FindStep(ApiDeltaStepNames.Select)!.Status.Should().Be(StepStatus.Completed);
    }

    [Fact]
    public async Task Restarting_the_whole_run_discards_what_was_kept()
    {
        Harness harness = new();
        harness.ProbeFailsOnceThenSucceeds();
        harness.RecoveryChooses(RecoveryChoice.RestartAll);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);
        run.FindStep(ApiDeltaStepNames.Select)!.Status.Should().Be(StepStatus.Completed);
    }

    [Fact]
    public async Task An_interrupted_run_is_offered_again_and_its_selection_comes_back()
    {
        Harness harness = new();
        harness.OffersInterruptedRun(ProductionHash);

        bool resumed = await harness.TryResumeAsync();

        resumed.Should().BeTrue();

        RunSelection? selection = harness.Context.Get<RunSelection>(ApiDeltaContextKeys.ResumedSelection);
        selection.Should().NotBeNull();
        selection.ProductionPath.Should().Be(ProductionPath);
    }

    [Fact]
    public async Task A_run_whose_file_has_changed_since_is_refused_rather_than_resumed()
    {
        Harness harness = new();

        // Le point de reprise se souvient d'une autre empreinte que celle du fichier d'aujourd'hui.
        harness.OffersInterruptedRun("0000000000000000000000000000000000");

        bool resumed = await harness.TryResumeAsync();

        resumed.Should().BeFalse(
            "une comparaison reprise sur un fichier modifié depuis porterait sur ce qui n'existe plus");

        harness.Context.Get<RunSelection>(ApiDeltaContextKeys.ResumedSelection).Should().BeNull();
        harness.Console.Output.Should().Contain("a été modifié");
    }

    /// <summary>
    /// Monte l'étape de sélection sur le vrai moteur d'étapes, avec une invite et une sonde de fichiers
    /// substituées : ce qui est éprouvé est le chemin réel, points de reprise et reprise sur erreur compris.
    /// </summary>
    private sealed class Harness
    {
        private readonly IApiDeltaPrompt _prompt = Substitute.For<IApiDeltaPrompt>();
        private readonly ISpecFileProbe _probe = Substitute.For<ISpecFileProbe>();
        private readonly IClock _clock = Substitute.For<IClock>();

        internal Harness()
        {
            _clock.UtcNow.Returns(Now);

            _prompt.AskForSelection(Arg.Any<RunSelection?>(), Arg.Any<CancellationToken>())
                .Returns(call => call.ArgAt<RunSelection?>(0) is { } resumed
                    ? Result.Success(resumed)
                    : Result.Success(Selection()));

            ProbeSucceeds();

            Recovery.AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>())
                .Returns(RecoveryChoice.Abort);

            Context = new ToolRunContext(
                new ToolRun(Guid.NewGuid(), "apidelta", Now, [ApiDeltaStepNames.Select]));
        }

        internal IRunCheckpointStore Store { get; } = Substitute.For<IRunCheckpointStore>();

        internal IRecoveryPrompt Recovery { get; } = Substitute.For<IRecoveryPrompt>();

        internal TestConsole Console { get; } = new TestConsole().Interactive();

        internal ToolRunContext Context { get; }

        internal void ProbeFails(string message) =>
            _probe.ProbeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Fail<SpecFileFacts>(FailureReason.InvalidInput, message)));

        internal void ProbeFailsOnceThenSucceeds()
        {
            int calls = 0;

            _probe.ProbeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult(calls++ == 0
                    ? Result.Fail<SpecFileFacts>(FailureReason.InvalidInput, "Ce fichier n'existe pas.")
                    : Result.Success(Facts(ProductionHash))));
        }

        internal void RecoveryChooses(RecoveryChoice choice) =>
            Recovery.AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>()).Returns(choice);

        internal void OffersInterruptedRun(string rememberedHash)
        {
            PersistedRun persisted = new(
                Context.Run.RunId,
                "apidelta",
                [
                    new PersistedSelectionEntry(ApiDeltaSelectionKinds.Mode, "Audit", "Audit"),
                    new PersistedSelectionEntry(
                        ApiDeltaSelectionKinds.Production,
                        ProductionPath,
                        "production.yaml · sha256:" + rememberedHash[..12]),
                    new PersistedSelectionEntry(
                        ApiDeltaSelectionKinds.Output, @"C:\travail\artefacts", @"C:\travail\artefacts"),
                ],
                [],
                Now,
                Now);

            Store.ListResumableAsync("apidelta", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<PersistedRun>>([persisted]));

            // « Reprendre » est le premier choix de l'invite.
            Console.Input.PushKey(ConsoleKey.Enter);
        }

        internal Task<bool> TryResumeAsync() =>
            new ApiDeltaResumeCoordinator(new ResumePrompt(Console), Store, _probe, Console)
                .TryResumeAsync("apidelta", Context, CancellationToken.None);

        internal Task<ToolRun> RunAsync()
        {
            SequentialStepRunner runner = new(
                _clock,
                Store,
                [new ApiDeltaCheckpointFactory(_clock)],
                Recovery,
                NullLogger<SequentialStepRunner>.Instance);

            return runner.RunAsync(
                Context,
                [
                    new SelectStep(
                        _prompt,
                        _probe,
                        Substitute.For<IRulesetStore>(),
                        Substitute.For<ISuppressionStore>(),
                        ApiDeltaOptions.Default),
                ],
                CancellationToken.None);
        }

        private void ProbeSucceeds() =>
            _probe.ProbeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success(Facts(ProductionHash))));

        private static SpecFileFacts Facts(string hash) => new(ProductionPath, 1024, hash);

        private static RunSelection Selection() =>
            new(RunMode.Audit, ProductionPath, string.Empty, @"C:\travail\artefacts");
    }
}
