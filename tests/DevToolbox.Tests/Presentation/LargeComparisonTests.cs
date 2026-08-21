using System.Diagnostics;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// La taille de référence : cinq groupes, cinq cents variables distinctes.
/// </summary>
public sealed class LargeComparisonTests
{
    private const int GroupCount = 5;
    private const int VariableCount = 500;

    private static readonly DateTimeOffset ReadAt = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Five_hundred_variables_across_five_groups_render_within_five_seconds()
    {
        // Le budget est volontairement généreux : il s'agit de vérifier que le chemin de rendu n'a pas de
        // coût pathologique, pas qu'il soit aussi rapide que possible.
        VariableComparison comparison = BuildLarge();

        using TestConsole console = new();
        console.Profile.Width = 200;
        console.Profile.Height = 50;

        ComparisonPresenter presenter = new(console, new ConsoleCapabilities(console));

        Stopwatch stopwatch = Stopwatch.StartNew();
        presenter.RenderPage(comparison, ComparisonViewOptions.Default, 1);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Every_one_of_the_five_hundred_rows_is_reachable_by_paging()
    {
        // Rester navigable, ce qui en pratique veut dire : chaque ligne reste atteignable.
        VariableComparison comparison = BuildLarge();
        IReadOnlyList<ComparisonRow> rows = comparison.VisibleRows(differencesOnly: false);

        int pageSize = ComparisonPager.PageSizeFor(50, ComparisonLayout.SideBySide);
        int pageCount = ComparisonPager.Page(rows, 1, pageSize).PageCount;

        List<string> seen = [];

        for (int page = 1; page <= pageCount; page++)
        {
            seen.AddRange(ComparisonPager.Page(rows, page, pageSize).Rows.Select(row => row.DisplayName));
        }

        seen.Should().HaveCount(VariableCount);
        seen.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_row_count_equals_the_distinct_name_union_of_the_source_groups()
    {
        // Toutes les variables comptabilisées, aucune omission silencieuse. Vérifié en comptant à partir des
        // sources plutôt qu'en se fiant au récapitulatif.
        List<VariableGroupSnapshot> snapshots = BuildSnapshots();

        HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase);

        foreach (VariableGroupSnapshot snapshot in snapshots)
        {
            foreach (string name in snapshot.Names)
            {
                expected.Add(name);
            }
        }

        VariableComparison comparison = ComparisonBuilder.Build(snapshots);

        comparison.Rows.Should().HaveCount(expected.Count);
        comparison.Summary.TotalVariables.Should().Be(expected.Count);
    }

    [Fact]
    public void Every_row_has_exactly_one_cell_per_group_with_no_gaps()
    {
        VariableComparison comparison = BuildLarge();

        comparison.Groups.Should().HaveCount(GroupCount);
        comparison.Rows.Should().OnlyContain(row => row.Cells.Count == GroupCount);
    }

    [Fact]
    public void The_stated_total_matches_what_is_actually_compared()
    {
        VariableComparison comparison = BuildLarge();

        comparison.Summary.TotalVariables.Should().Be(comparison.Rows.Count);
        (comparison.Summary.PresentEverywhere + comparison.Summary.MissingSomewhere)
            .Should().Be(comparison.Rows.Count);
    }

    private static VariableComparison BuildLarge() => ComparisonBuilder.Build(BuildSnapshots());

    private static List<VariableGroupSnapshot> BuildSnapshots()
    {
        List<VariableGroupSnapshot> snapshots = [];

        for (int group = 1; group <= GroupCount; group++)
        {
            List<VariableEntry> entries = [];

            for (int variable = 1; variable <= VariableCount; variable++)
            {
                // Une variable sur cinq manque dans un groupe sur trois, pour que la comparaison présente une
                // vraie dérive plutôt que cinq colonnes identiques.
                if (variable % 5 == 0 && group % 3 == 0)
                {
                    continue;
                }

                entries.Add(new VariableEntry(
                    $"Var__{variable:D3}",
                    variable % 7 == 0 ? null : $"value-{group}-{variable}",
                    IsSecret: variable % 7 == 0,
                    IsReadOnly: variable % 11 == 0));
            }

            snapshots.Add(new VariableGroupSnapshot(
                new VariableGroupSummary(
                    group, $"group-{group}", null, VariableGroupOrigin.Ordinary, false),
                entries,
                ReadAt));
        }

        return snapshots;
    }
}
