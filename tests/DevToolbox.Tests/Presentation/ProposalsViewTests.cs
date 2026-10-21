using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// Ce que l'écran du chiffrage montre, et ce qu'il refuse de laisser croire.
/// </summary>
public sealed class ProposalsViewTests
{
    [Fact]
    public void The_panel_says_the_plan_changes_nothing()
    {
        // L'écran précède l'application de plusieurs tranches, et même après elle le plan restera un
        // devis. Le dire au premier écran évite qu'un développeur croie le nettoyage fait.
        (TestConsole console, string output) = Render(Plan([Proposal(SafetyClass.Safe, 1785)]));

        output.Should().Contain("aucun fichier n'a été modifié");

        console.Dispose();
    }

    [Fact]
    public void The_panel_counts_safe_and_review_required_apart()
    {
        (TestConsole console, string output) = Render(
            Plan([Proposal(SafetyClass.Safe, 1785), Proposal(SafetyClass.ReviewRequired, 240)]));

        output.Should().Contain("propositions prouvées sûres")
            .And.Contain("propositions à relire")
            .And.Contain("économie estimée")
            .And.Contain("2025");

        console.Dispose();
    }

    [Fact]
    public void A_measured_repetition_no_proposal_covers_is_announced_on_the_panel()
    {
        // Un plan sans proposition ne veut pas dire un fichier sans répétition, et c'est la confusion
        // que cette ligne existe pour empêcher.
        (TestConsole console, string output) = Render(
            new OptimizationPlan([Group(SafetyClass.Unsafe, 151)], [], GrowthCheck.For(1000, 0, 0)));

        output.Should().Contain("ne sont couvertes par aucune proposition");

        console.Dispose();
    }

    [Fact]
    public void A_projection_beyond_what_is_tolerated_is_announced_as_unpublishable()
    {
        (TestConsole console, string output) = Render(
            new OptimizationPlan([], [], GrowthCheck.For(1000, -500, 0)));

        output.Should().Contain("dépasserait la croissance tolérée");

        console.Dispose();
    }

    [Fact]
    public void A_safe_proposal_shows_its_destination_its_saving_and_how_to_undo_it()
    {
        (TestConsole console, string output) = Render(
            Plan([Proposal(SafetyClass.Safe, 1785)]),
            choices: 0);

        output.Should().Contain("OPT-002")
            .And.Contain("/components/parameters/XServiceEmetteur")
            .And.Contain("1785")
            .And.Contain("rentable à partir de 2 occurrences")
            .And.Contain("pour la défaire");

        console.Dispose();
    }

    [Fact]
    public void A_proposal_that_needs_review_says_why_before_anything_else_about_it()
    {
        (TestConsole console, string output) = Render(
            Plan([Proposal(SafetyClass.ReviewRequired, 240)]),
            choices: 1);

        output.Should().Contain("à relire")
            .And.Contain("n'a pas pu établir");

        console.Dispose();
    }

    [Fact]
    public void An_application_whose_verification_failed_says_nothing_was_published()
    {
        // Un développeur à qui l'on ne dit rien suppose que l'application a eu lieu. C'est la supposition
        // la plus coûteuse que cet écran puisse laisser faire.
        (TestConsole console, string output) = Render(
            Plan([Proposal(SafetyClass.Safe, 1785)]),
            applied: new OptimizationOutcome(
                ReadOnlyMemory<byte>.Empty,
                [Proposal(SafetyClass.Safe, 1785)],
                [],
                new VerificationResult(true, true, true, false, ["le contrat diffère en /paths"])));

        output.Should().Contain("Rien n'a été publié")
            .And.Contain("le contrat a changé");

        console.Dispose();
    }

    [Fact]
    public void An_application_that_held_says_what_it_established_and_what_it_set_aside()
    {
        (TestConsole console, string output) = Render(
            Plan([Proposal(SafetyClass.Safe, 1785)]),
            applied: new OptimizationOutcome(
                new byte[64],
                [Proposal(SafetyClass.Safe, 1785)],
                [
                    new DeclinedTransformation(
                        Proposal(SafetyClass.ReviewRequired, 240),
                        "La règle OPT-008 demande une relecture et n'a pas été autorisée nommément."),
                ],
                new VerificationResult(true, true, true, true, [])));

        output.Should().Contain("transformations appliquées")
            .And.Contain("écartée")
            .And.Contain("OPT-008")
            .And.Contain("Copie vérifiée")
            .And.Contain("le contrat est inchangé");

        console.Dispose();
    }

    private static OptimizationPlan Plan(IReadOnlyList<Transformation> proposals) =>
        new(
            [Group(SafetyClass.Safe, 1785)],
            proposals,
            GrowthCheck.For(12920, proposals.Sum(proposal => proposal.EstimatedBytesSaved), 0));

    private static RedundancyGroup Group(SafetyClass safety, long saving) =>
        new(
            "AUD-100",
            "abc",
            "strict",
            CandidateKind.Parameter,
            DuplicateTier.Exact,
            [new Occurrence(Pointer, 200), new Occurrence(Pointer, 200)],
            new SavingsEstimate(saving * 2, saving, 2, safety));

    private static Transformation Proposal(SafetyClass safety, long saving) =>
        new(
            safety == SafetyClass.Safe ? "OPT-002" : "OPT-008",
            safety,
            [Pointer],
            SpecPointer.Root.Child("components").Child("parameters").Child("XServiceEmetteur"),
            safety == SafetyClass.Safe ? "abc" : null,
            new ReversalDescription("réinsérer le paramètre à chaque origine", Reversible: true),
            TransformationState.Proposed)
        {
            Summary = "Extraire le paramètre répété.",
            FromRuleId = "AUD-100",
            EstimatedBytesSaved = saving,
            BreakEvenOccurrences = 2,
            ReviewReason = safety == SafetyClass.Safe
                ? null
                : "L'outil n'a pas pu établir que ce remède laisse les appelants inchangés.",
        };

    private static SpecPointer Pointer =>
        SpecPointer.Root.Child("paths").Child("/un").Child("get").Child("parameters").Item(0);

    /// <summary>
    /// Affiche le chiffrage, en répondant au besoin à l'invite de navigation.
    /// </summary>
    /// <param name="plan">Le plan à montrer.</param>
    /// <param name="applied">Ce que l'application a produit, quand l'essai en monte une.</param>
    /// <param name="choices">
    /// Le rang du choix à retenir, ou <see langword="null"/> pour fermer l'écran sans entrer dans le
    /// détail.
    /// </param>
    private static (TestConsole Console, string Output) Render(
        OptimizationPlan plan,
        int? choices = null,
        OptimizationOutcome? applied = null)
    {
        TestConsole console = new();
        console.Interactive();

        if (plan.Groups.Count > 0)
        {
            for (int step = 0; step < (choices ?? 3); step++)
            {
                console.Input.PushKey(ConsoleKey.DownArrow);
            }

            console.Input.PushKey(ConsoleKey.Enter);

            if (choices is not null)
            {
                // Puis revenir, pour fermer l'écran.
                for (int step = 0; step < 3; step++)
                {
                    console.Input.PushKey(ConsoleKey.DownArrow);
                }

                console.Input.PushKey(ConsoleKey.Enter);
            }
        }

        new ProposalsView(console).Show(plan, 12920, applied, CancellationToken.None);

        return (console, console.Output);
    }
}
