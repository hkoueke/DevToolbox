using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// La disposition empilée doit porter exactement ce que porte la grille : aucun groupe abandonné, aucune
/// variable abandonnée, aucune valeur affichée.
/// </summary>
public sealed class StackedRendererTests
{
    private const string HiddenValue = "value-that-must-never-be-rendered";

    private static readonly DateTimeOffset ReadAt = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_variable_and_every_group_appears_in_the_stacked_layout()
    {
        // Toutes les variables et tous les groupes sélectionnés restent visibles.
        string output = RenderStacked(ManyGroups(8));

        foreach (string variable in new[] { "Api__BaseUrl", "Api__Key", "OnlyInFirst" })
        {
            output.Should().Contain(variable);
        }

        for (int index = 1; index <= 8; index++)
        {
            output.Should().Contain($"group-{index}");
        }
    }

    [Fact]
    public void The_stacked_layout_shows_the_same_states_as_the_grid()
    {
        // États, symboles et mots identiques dans les deux dispositions.
        VariableComparison comparison = ManyGroups(3);

        string grid = Render(comparison, ComparisonLayout.SideBySide, width: 400);
        string stacked = Render(comparison, ComparisonLayout.Stacked, width: 400);

        foreach (string state in new[] { "set", "missing", "secret" })
        {
            grid.Should().Contain(state);
            stacked.Should().Contain(state);
        }
    }

    [Fact]
    public void No_value_appears_in_the_stacked_layout_either()
    {
        // La garantie tient dans les deux dispositions, puisqu'une cellule ne porte de toute façon aucune
        // valeur.
        RenderStacked(ManyGroups(6)).Should().NotContain(HiddenValue);
    }

    [Fact]
    public void The_legend_is_rendered_with_the_stacked_layout_too()
    {
        // La légende accompagne la comparaison, quelle que soit la disposition employée.
        string output = RenderStacked(ManyGroups(6));

        output.Should().Contain("Legend");
        output.Should().Contain("sourced from a key vault");
    }

    [Fact]
    public void A_terminal_too_narrow_for_anything_says_so_instead_of_rendering_a_partial_result()
    {
        // Jamais de comparaison partielle qui serait lue comme complète.
        string output = Render(ManyGroups(6), ComparisonLayout.SideBySide, width: 20);

        // Les espaces sont normalisés car, à cette largeur, l'explication elle-même se replie. Ce qui compte
        // est qu'elle soit dite, pas l'endroit où les lignes se coupent.
        Flatten(output).Should().Contain("too narrow to show every group and every variable");
        output.Should().NotContain("Api__BaseUrl");
    }

    [Fact]
    public void Casing_divergence_is_warned_about_and_the_spellings_are_shown()
    {
        // Noms ne différant que par la casse.
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("API_KEY", "x")),
            Snapshot("prod", ("Api_Key", "x")),
        ]);

        string output = Render(comparison, ComparisonLayout.SideBySide, width: 200);

        output.Should().Contain("spelled differently");
        output.Should().Contain("API_KEY / Api_Key");
    }

    /// <summary>Réduit les suites d'espaces pour qu'une vérification ne dépende pas des retours à la ligne.</summary>
    private static string Flatten(string output) =>
        string.Join(" ", output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string RenderStacked(VariableComparison comparison) =>
        Render(comparison, ComparisonLayout.Stacked, width: 120);

    private static string Render(VariableComparison comparison, ComparisonLayout layout, int width)
    {
        using TestConsole console = new();
        console.Profile.Width = width;
        console.Profile.Height = 200;

        ComparisonPresenter presenter = new(console, new ConsoleCapabilities(console));
        presenter.RenderPage(comparison, new ComparisonViewOptions(layout, false), 1);

        return console.Output;
    }

    private static VariableComparison ManyGroups(int count)
    {
        List<VariableGroupSnapshot> snapshots = [];

        for (int index = 1; index <= count; index++)
        {
            List<VariableEntry> entries =
            [
                new VariableEntry("Api__BaseUrl", HiddenValue, false, false),
                new VariableEntry("Api__Key", null, IsSecret: true, IsReadOnly: false),
            ];

            if (index == 1)
            {
                entries.Add(new VariableEntry("OnlyInFirst", HiddenValue, false, IsReadOnly: true));
            }

            snapshots.Add(new VariableGroupSnapshot(
                new VariableGroupSummary(
                    index,
                    $"group-{index}",
                    null,
                    index == 2 ? VariableGroupOrigin.KeyVaultBacked : VariableGroupOrigin.Ordinary,
                    false),
                entries,
                ReadAt));
        }

        return ComparisonBuilder.Build(snapshots);
    }

    private static VariableGroupSnapshot Snapshot(
        string name,
        params (string Name, string Value)[] variables) =>
        new(
            new VariableGroupSummary(
                name.GetHashCode(StringComparison.Ordinal),
                name,
                null,
                VariableGroupOrigin.Ordinary,
                false),
            variables.Select(v => new VariableEntry(v.Name, v.Value, false, false)),
            ReadAt);
}
