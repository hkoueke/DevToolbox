using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// Ce que la comparaison affiche et, plus important encore, ce qu'elle n'affiche jamais.
/// </summary>
public sealed class ComparisonPresenterTests
{
    private const string DevSecret = "dev-secret-value-must-never-appear";
    private const string ProdValue = "prod-value-must-never-appear";

    private static readonly DateTimeOffset ReadAt = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void No_variable_value_appears_anywhere_in_the_comparison()
    {
        // Une valeur n'est visible qu'après ouverture explicite d'une vue de détail. La comparaison
        // elle-même ne montre que des états.
        (TestConsole console, string output) = Render(ComparisonViewOptions.Default);

        output.Should().NotContain(DevSecret);
        output.Should().NotContain(ProdValue);

        console.Dispose();
    }

    [Fact]
    public void Every_state_is_named_in_words_so_it_survives_without_colour()
    {
        // Identifiable sans couleur. La console de test n'émet aucun code ANSI : ce qui reste est donc
        // exactement ce qu'affiche un terminal monochrome.
        (TestConsole console, string output) = Render(ComparisonViewOptions.Default);

        output.Should().Contain("définie");
        output.Should().Contain("absente");
        output.Should().Contain("vide");
        output.Should().Contain("secrète");

        console.Dispose();
    }

    [Fact]
    public void The_legend_defines_every_state_and_qualifier()
    {
        (TestConsole console, string output) = Render(ComparisonViewOptions.Default);

        output.Should().Contain("Légende");
        output.Should().Contain("issue d'un coffre de clés");
        output.Should().Contain("marquée en lecture seule dans ce groupe");
        output.Should().Contain("les valeurs lisibles diffèrent");

        console.Dispose();
    }

    [Fact]
    public void Every_variable_and_every_group_is_present_in_the_output()
    {
        // Rien n'est omis en silence.
        (TestConsole console, string output) = Render(ComparisonViewOptions.Default);

        output.Should().Contain("Api__BaseUrl");
        output.Should().Contain("Api__Key");
        output.Should().Contain("Feature__X");
        output.Should().Contain("OnlyInDev");
        output.Should().Contain("dev");
        output.Should().Contain("prod");
        output.Should().Contain("4 variables comparées sur 2 groupes");

        console.Dispose();
    }

    [Fact]
    public void The_differences_only_filter_hides_identical_rows_and_states_how_many()
    {
        // Le filtre des seules différences.
        (TestConsole console, string output) =
            Render(new ComparisonViewOptions(ComparisonLayout.Auto, DifferencesOnly: true));

        output.Should().Contain("ligne(s) identique(s) masquée(s)");

        console.Dispose();
    }

    [Fact]
    public void The_read_time_is_stated_so_the_developer_knows_what_moment_this_reflects()
    {
        (TestConsole console, string output) = Render(ComparisonViewOptions.Default);

        output.Should().Contain("au " + ReadAt.ToLocalTime().ToString("u", null));

        console.Dispose();
    }

    private static (TestConsole Console, string Output) Render(ComparisonViewOptions options)
    {
        TestConsole console = new();
        console.Profile.Width = 200;

        ComparisonPresenter presenter = new(console, new ConsoleCapabilities(console));
        presenter.Render(BuildComparison(), options);

        return (console, console.Output);
    }

    private static VariableComparison BuildComparison()
    {
        VariableGroupSnapshot dev = new(
            new VariableGroupSummary(1, "dev", null, VariableGroupOrigin.Ordinary, false),
            [
                new VariableEntry("Api__BaseUrl", "https://same.example", false, false),
                new VariableEntry("Api__Key", DevSecret, IsSecret: true, IsReadOnly: false),
                new VariableEntry("Feature__X", string.Empty, false, IsReadOnly: true),
                new VariableEntry("OnlyInDev", "x", false, false),
            ],
            ReadAt);

        VariableGroupSnapshot prod = new(
            new VariableGroupSummary(2, "prod", null, VariableGroupOrigin.KeyVaultBacked, false),
            [
                new VariableEntry("Api__BaseUrl", "https://same.example", false, false),
                new VariableEntry("Api__Key", ProdValue, IsSecret: true, IsReadOnly: false),
                new VariableEntry("Feature__X", "set-here", false, false),
            ],
            ReadAt);

        return ComparisonBuilder.Build([dev, prod]);
    }
}
