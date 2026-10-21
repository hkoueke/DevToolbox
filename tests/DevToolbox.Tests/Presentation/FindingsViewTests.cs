using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// Ce que l'écran de l'examen montre, et ce qu'il refuse de taire.
/// </summary>
public sealed class FindingsViewTests
{
    [Fact]
    public void A_rule_that_could_not_be_evaluated_is_on_the_panel_and_not_in_a_drill_down()
    {
        // Enterrer une règle non évaluée dans un sous-écran laisserait croire qu'un examen sans constat
        // est un examen propre. C'est l'erreur que cet outil existe pour ne pas commettre.
        (TestConsole console, string output) = Render(
            new AuditResult(
                [],
                [
                    new RuleOutcome("AUD-002", RuleState.NotEvaluated, 0, "Les références n'ont pas été résolues."),
                ]));

        output.Should().Contain("non évaluée")
            .And.Contain("AUD-002")
            .And.Contain("Les références n'ont pas été résolues.")
            .And.Contain("n'est pas complet");

        console.Dispose();
    }

    [Fact]
    public void A_truncated_rule_says_so_on_the_panel()
    {
        (TestConsole console, string output) = Render(
            new AuditResult(
                [],
                [new RuleOutcome("AUD-104", RuleState.Truncated, 0, "Un groupe dépassait la borne.")]));

        output.Should().Contain("rendue incomplète").And.Contain("AUD-104");

        // Une troncature n'est pas une règle non exécutée : le rapport reste complet.
        output.Should().NotContain("n'est pas complet");

        console.Dispose();
    }

    [Fact]
    public void An_audit_whose_rules_all_ran_announces_nothing_about_completeness()
    {
        (TestConsole console, string output) = Render(
            new AuditResult([], [new RuleOutcome("AUD-100", RuleState.Evaluated, 0)]));

        output.Should().NotContain("non évaluée").And.NotContain("n'est pas complet");

        console.Dispose();
    }

    [Fact]
    public void The_panel_counts_by_severity_and_names_each_in_words()
    {
        // Identifiable sans couleur : la console de test n'émet aucun code ANSI.
        (TestConsole console, string output) = Render(
            new AuditResult(
                [Finding("AUD-100", FindingSeverity.Warning, 1785)],
                [new RuleOutcome("AUD-100", RuleState.Evaluated, 1)]));

        output.Should().Contain("à corriger")
            .And.Contain("erreurs")
            .And.Contain("pour information")
            .And.Contain("octets qu'un nettoyage ferait gagner")
            .And.Contain("1785");

        console.Dispose();
    }

    [Fact]
    public void A_deduction_is_shown_as_a_deduction()
    {
        (TestConsole console, string output) = Render(
            new AuditResult(
                [
                    Finding("AUD-206", FindingSeverity.Information, null) with
                    {
                        Evidence = new FindingEvidence(Reasoning: "Déduit du nom de la propriété."),
                    },
                ],
                [new RuleOutcome("AUD-206", RuleState.Evaluated, 1)]),
            everything: true);

        output.Should().Contain("déduction").And.Contain("Déduit du nom de la propriété.");

        console.Dispose();
    }

    [Fact]
    public void A_finding_shows_its_rule_its_location_and_what_the_remedy_is_worth()
    {
        (TestConsole console, string output) = Render(
            new AuditResult(
                [Finding("AUD-100", FindingSeverity.Warning, 1785)],
                [new RuleOutcome("AUD-100", RuleState.Evaluated, 1)]),
            everything: true);

        output.Should().Contain("AUD-100")
            .And.Contain("/paths/~1un/get")
            .And.Contain("remède prouvé sûr")
            .And.Contain("économie estimée");

        console.Dispose();
    }

    private static Finding Finding(string ruleId, FindingSeverity severity, long? saved) =>
        new(
            ruleId,
            severity,
            SafetyClass.Safe,
            "Le même paramètre est écrit 14 fois à l'identique.",
            SpecPointer.Root.Child("paths").Child("/un").Child("get"),
            "L'extraire dans « components.parameters ».")
        {
            EstimatedBytesSaved = saved,
            StrictEquivalenceEstablished = true,
        };

    /// <summary>
    /// Affiche l'examen, en répondant au besoin aux invites de navigation.
    /// </summary>
    /// <remarks>
    /// Les touches sont poussées à l'avance : « Entrée » retient le premier choix, et le dernier retour
    /// ferme l'écran. Sans elles, l'invite attendrait une saisie qui ne viendra pas.
    /// </remarks>
    private static (TestConsole Console, string Output) Render(
        AuditResult audit,
        bool everything = false)
    {
        TestConsole console = new();
        console.Interactive();

        if (everything)
        {
            // Premier choix : tout voir. Puis revenir, deux fois, pour quitter le détail et l'écran.
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.Enter);
        }
        else if (audit.Findings.Count > 0)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.Enter);
        }

        new FindingsView(console).Show(audit, CancellationToken.None);

        return (console, console.Output);
    }
}
