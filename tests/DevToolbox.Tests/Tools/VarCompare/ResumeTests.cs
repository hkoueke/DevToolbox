using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Core.Steps;
using DevToolbox.Tools.VarCompare.Presentation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Tools.VarCompare;

/// <summary>
/// Ce qu'une reprise après redémarrage doit réellement faire : relire, et aboutir.
/// </summary>
/// <remarks>
/// Ces tests existent parce que leur absence a coûté cher. Une reprise déclarait achevées des étapes dont le
/// résultat ne vit qu'en mémoire ; l'étape de lecture ne trouvait alors aucune sélection et échouait
/// aussitôt sur « Aucun groupe n'a encore été sélectionné ». Le développeur n'avait d'autre issue que de
/// tout recommencer, ce que la reprise était précisément censée lui épargner.
/// </remarks>
public sealed class ResumeTests
{
    private static readonly string[] StepNames =
    [
        IdentifyProjectStep.StepName,
        ListGroupsStep.StepName,
        SelectGroupsStep.StepName,
        RetrieveGroupsStep.StepName,
        BuildComparisonStep.StepName,
    ];

    [Fact]
    public async Task Resuming_a_run_whose_selection_was_already_made_gets_all_the_way_to_a_comparison()
    {
        Harness harness = new();
        ToolRunContext context = await harness.ResumeAsync();

        ToolRun run = await harness.RunAsync(context);

        run.Outcome.Should().Be(RunOutcome.Success);

        // Les deux groupes retenus sont relus, jamais restaurés de mémoire.
        harness.RetrievedIds.Should().BeEquivalentTo([1, 2]);

        // Et le développeur n'est pas réinterrogé sur une sélection qu'il a déjà faite.
        await harness.Chooser.DidNotReceive().ChooseAsync(
            Arg.Any<IReadOnlyList<VariableGroupSummary>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Only_the_project_step_is_carried_over_because_it_is_the_only_one_reseeded()
    {
        Harness harness = new();
        ToolRunContext context = await harness.ResumeAsync();

        // Déclarer achevée une étape dont rien ne redépose le résultat reviendrait à promettre au moteur une
        // valeur absente du contexte.
        Status(context, IdentifyProjectStep.StepName).Should().Be(StepStatus.Completed);
        Status(context, ListGroupsStep.StepName).Should().Be(StepStatus.Pending);
        Status(context, SelectGroupsStep.StepName).Should().Be(StepStatus.Pending);
        Status(context, RetrieveGroupsStep.StepName).Should().Be(StepStatus.Pending);

        await harness.RunAsync(context);

        // La liste, elle, est bel et bien relue.
        harness.ListedGroups.Should().Be(1);

        // Mais le projet n'est pas redemandé : son résultat a été redéposé.
        await harness.Gateway.DidNotReceive().ListProjectsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_remembered_group_that_has_since_vanished_is_dropped_out_loud()
    {
        Harness harness = new();
        ToolRunContext context = await harness.ResumeAsync(
        [
            new PersistedGroupSelection(1, "alpha"),
            new PersistedGroupSelection(2, "beta"),
            new PersistedGroupSelection(99, "disparu"),
        ]);

        ToolRun run = await harness.RunAsync(context);

        run.Outcome.Should().Be(RunOutcome.Success);

        // Écarter en silence laisserait croire à une colonne vide plutôt qu'à un groupe absent.
        harness.Console.Output.Should().Contain("disparu");
        harness.RetrievedIds.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task A_resumed_selection_worn_down_below_two_groups_asks_the_developer_again()
    {
        Harness harness = new();
        ToolRunContext context = await harness.ResumeAsync(
            [new PersistedGroupSelection(1, "alpha"), new PersistedGroupSelection(99, "disparu")]);

        await harness.RunAsync(context);

        // Un seul groupe ne fait pas une comparaison : redemander vaut mieux qu'échouer.
        await harness.Chooser.Received(1).ChooseAsync(
            Arg.Any<IReadOnlyList<VariableGroupSummary>>(), Arg.Any<CancellationToken>());
    }

    private static StepStatus Status(ToolRunContext context, string name) =>
        context.Run.FindStep(name)!.Status;

    /// <summary>Le vrai moteur, les vraies étapes, la vraie reprise ; seule la passerelle est simulée.</summary>
    private sealed class Harness
    {
        private static readonly ProjectIdentifier Project = new("Contoso");

        private static VariableGroupSummary Group(int id, string name) =>
            new(id, name, Description: null, VariableGroupOrigin.Ordinary, IsShared: false);

        private readonly IRunCheckpointStore _store = Substitute.For<IRunCheckpointStore>();
        private readonly IClock _clock = Substitute.For<IClock>();
        private ResumeCoordinator? _coordinator;

        internal Harness()
        {
            _clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);

            Gateway.ListGroupsAsync(Arg.Any<ProjectIdentifier>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    ListedGroups++;
                    return Task.FromResult(Result.Success<IReadOnlyList<VariableGroupSummary>>(
                        [Group(1, "alpha"), Group(2, "beta")]));
                });

            Gateway.GetGroupAsync(
                    Arg.Any<ProjectIdentifier>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    int id = call.ArgAt<int>(1);

                    lock (RetrievedIds)
                    {
                        RetrievedIds.Add(id);
                    }

                    return Task.FromResult(Result.Success(new VariableGroupSnapshot(
                        Group(id, $"groupe {id}"), [], DateTimeOffset.UnixEpoch)));
                });

            Chooser.ChooseAsync(Arg.Any<IReadOnlyList<VariableGroupSummary>>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyList<VariableGroupSummary>>(
                    [Group(1, "alpha"), Group(2, "beta")]));
        }

        internal IVariableGroupGateway Gateway { get; } = Substitute.For<IVariableGroupGateway>();

        internal IGroupChooser Chooser { get; } = Substitute.For<IGroupChooser>();

        internal TestConsole Console { get; } = new TestConsole().Interactive();

        internal int ListedGroups { get; private set; }

        internal List<int> RetrievedIds { get; } = [];

        /// <summary>
        /// Reprend une exécution interrompue par le chemin public réel : le magasin propose, l'invite
        /// demande, le coordinateur amorce.
        /// </summary>
        internal async Task<ToolRunContext> ResumeAsync(
            IReadOnlyList<PersistedGroupSelection>? remembered = null)
        {
            ToolRun run = new(Guid.NewGuid(), "varcompare", DateTimeOffset.UnixEpoch, StepNames);
            ToolRunContext context = new(run);

            PersistedRun persisted = new(
                run.RunId,
                "varcompare",
                "DefaultCollection",
                Project.Name,
                remembered ??
                    [new PersistedGroupSelection(1, "alpha"), new PersistedGroupSelection(2, "beta")],
                // Le cas qui échouait : le point de reprise est écrit après chaque étape réussie, donc
                // aussi après la sélection.
                [IdentifyProjectStep.StepName, ListGroupsStep.StepName, SelectGroupsStep.StepName],
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch);

            _store.ListResumableAsync("varcompare", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<PersistedRun>>([persisted]));

            _coordinator = new ResumeCoordinator(new ResumePrompt(Console), _store, Console);

            // « Reprendre » est le premier choix de l'invite.
            Console.Input.PushKey(ConsoleKey.Enter);

            bool resumed = await _coordinator.TryResumeAsync("varcompare", context, CancellationToken.None);
            resumed.Should().BeTrue();

            return context;
        }

        /// <summary>
        /// Une étape en échec renonce au lieu de relancer. Le substitut rendrait sinon
        /// <see cref="RecoveryChoice.RetryStep"/>, valeur zéro de l'énumération, et une étape qui échoue
        /// toujours ferait tourner le moteur sans fin : un test suspendu au lieu d'un test rouge.
        /// </summary>
        private static IRecoveryPrompt AbortingPrompt()
        {
            IRecoveryPrompt prompt = Substitute.For<IRecoveryPrompt>();
            prompt.AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(RecoveryChoice.Abort));

            return prompt;
        }

        internal Task<ToolRun> RunAsync(ToolRunContext context)
        {
            SequentialStepRunner runner = new(
                _clock,
                _store,
                Substitute.For<IRunCheckpointFactory>(),
                AbortingPrompt(),
                NullLogger<SequentialStepRunner>.Instance);

            IReadOnlyList<IToolStep> steps =
            [
                new IdentifyProjectStep(
                    Gateway, Substitute.For<IWorkingFolderInspector>(), Substitute.For<IProjectChooser>()),
                new ListGroupsStep(Gateway),
                new SelectGroupsStep(Chooser, _coordinator!),
                new RetrieveGroupsStep(Gateway, Substitute.For<IRetrievalProgress>(), _clock, 2),
                new BuildComparisonStep(),
            ];

            return runner.RunAsync(context, steps, CancellationToken.None);
        }
    }
}
