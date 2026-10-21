using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// Ce que les écrans disent encore quand le terminal ne sait ni les couleurs, ni l'Unicode, ni la largeur.
/// </summary>
/// <remarks>
/// La gravité et la sûreté sont portées par des mots, non par des couleurs. Un terminal de journal
/// d'intégration, une session en ASCII, un relecteur daltonien : dans les trois cas la couleur n'arrive
/// pas, et un écran qui n'aurait dit « rouge » n'aurait rien dit. Ces essais le constatent en coupant
/// tout ce qui peut être coupé.
/// </remarks>
public sealed class DegradationTests
{
    [Fact]
    public void Severity_survives_without_colour()
    {
        string output = Findings(
            Bare(), Finding(FindingSeverity.Error, SafetyClass.Unsafe), everything: true);

        output.Should().Contain("ERREUR", "la gravité se lit en tête du constat")
            .And.Contain("erreurs", "et se compte sur le panneau");

        output.Should().NotContain("[red]", "le balisage ne doit pas fuir dans la sortie");
    }

    [Fact]
    public void Safety_survives_without_colour()
    {
        // Les trois classes se lisent en mots : c'est la seule chose qui décide si un remède peut être
        // appliqué, et elle ne doit pas dépendre d'un terminal.
        Bare().Dispose();

        foreach ((SafetyClass safety, string word) in new[]
        {
            (SafetyClass.Safe, "remède prouvé sûr"),
            (SafetyClass.ReviewRequired, "remède à relire"),
            (SafetyClass.Unsafe, "signalé seulement"),
        })
        {
            TestConsole console = Bare();

            Findings(console, Finding(FindingSeverity.Warning, safety), everything: true)
                .Should().Contain(word);

            console.Dispose();
        }
    }

    [Fact]
    public void A_narrow_terminal_still_carries_the_rule_and_the_verdict()
    {
        TestConsole console = Bare(width: 40);

        string output = Findings(
            console, Finding(FindingSeverity.Warning, SafetyClass.Safe), everything: true);

        // La règle et l'emplacement tiennent sur quarante colonnes ; le reste se replie, et c'est
        // acceptable tant que l'identifiant reste lisible.
        output.Should().Contain("AUD-100");

        console.Dispose();
    }

    [Fact]
    public void An_ascii_only_terminal_loses_no_meaning()
    {
        TestConsole console = Bare(ascii: true);

        string output = Findings(
            console, Finding(FindingSeverity.Error, SafetyClass.Safe), everything: true);

        output.Should().Contain("ERREUR").And.Contain("à corriger");

        console.Dispose();
    }

    [Fact]
    public void An_incomplete_audit_says_so_in_words_on_a_bare_terminal()
    {
        // La ligne la plus importante de l'écran, et celle qui ne doit en aucun cas dépendre d'une
        // couleur : une règle qui n'a pas tourné n'a rien trouvé, et ce n'est pas la même nouvelle.
        TestConsole console = Bare(ascii: true, width: 40);

        AuditResult audit = new(
            [],
            [new RuleOutcome("AUD-002", RuleState.NotEvaluated, 0, "Les références n'ont pas été résolues.")]);

        new FindingsView(console).Show(audit, CancellationToken.None);

        console.Output.Should().Contain("non évaluée")
            .And.Contain("AUD-002")
            .And.Contain("n'est pas complet");

        console.Dispose();
    }

    [Fact]
    public void A_plan_that_cannot_be_published_says_it_in_words()
    {
        TestConsole console = Bare(ascii: true);

        OptimizationPlan plan = new([], [], GrowthCheck.For(1000, -500, 0));

        new ProposalsView(console).Show(plan, 1000, null, CancellationToken.None);

        console.Output.Should().Contain("dépasserait la croissance tolérée");

        console.Dispose();
    }

    /// <summary>
    /// Une console aussi dépouillée que possible : sans couleur, sans Unicode, et étroite.
    /// </summary>
    /// <remarks>
    /// <see cref="ColorSystem.NoColors"/> plutôt qu'un simple drapeau : Spectre écrit alors du texte nu,
    /// si bien qu'un balisage oublié apparaîtrait tel quel dans la sortie — et l'essai le verrait.
    /// </remarks>
    private static TestConsole Bare(bool ascii = false, int width = 120)
    {
        TestConsole console = new();

        console.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
        console.Profile.Capabilities.Unicode = !ascii;
        // ANSI reste : un menu ne fonctionne pas sans lui, et la coquille refuse déjà un terminal non
        // interactif avec son propre code de sortie. Ce qui est coupé ici est ce dont le sens ne doit
        // jamais dépendre — la couleur et l'Unicode.
        console.Profile.Width = width;

        return console;
    }

    /// <summary>Affiche un constat et rend la sortie.</summary>
    private static string Findings(TestConsole console, Finding finding, bool everything = false)
    {
        console.Interactive();

        if (everything)
        {
            console.Input.PushKey(ConsoleKey.Enter);
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.Enter);
        }

        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        new FindingsView(console).Show(
            new AuditResult([finding], [new RuleOutcome(finding.RuleId, RuleState.Evaluated, 1)]),
            CancellationToken.None);

        return console.Output;
    }

    private static Finding Finding(FindingSeverity severity, SafetyClass safety) =>
        new(
            "AUD-100",
            severity,
            safety,
            "Le même paramètre est écrit 14 fois à l'identique.",
            SpecPointer.Root.Child("paths").Child("/un").Child("get"),
            "L'extraire dans « components.parameters ».")
        {
            EstimatedBytesSaved = 1785,
            StrictEquivalenceEstablished = safety == SafetyClass.Safe,
        };
}
