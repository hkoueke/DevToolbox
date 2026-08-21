using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.VarCompare;

/// <summary>
/// Choix de la disposition, filtre des seules différences et pagination.
/// </summary>
public sealed class LayoutAndFilterTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_differences_only_filter_hides_identical_rows_and_reports_how_many()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("Same", "x"), ("Differs", "a"), ("OnlyInDev", "z")),
            Snapshot("prod", ("Same", "x"), ("Differs", "b")),
        ]);

        comparison.VisibleRows(differencesOnly: false).Should().HaveCount(3);

        IReadOnlyList<ComparisonRow> filtered = comparison.VisibleRows(differencesOnly: true);

        filtered.Select(row => row.DisplayName).Should().BeEquivalentTo("Differs", "OnlyInDev");
        comparison.HiddenRowCount(differencesOnly: true).Should().Be(1);
        comparison.HiddenRowCount(differencesOnly: false).Should().Be(0);
    }

    [Fact]
    public void A_wide_terminal_keeps_the_side_by_side_grid_without_warning()
    {
        VariableComparison comparison = TwoGroups();

        LayoutDecision decision = LayoutSelector.Decide(ComparisonLayout.Auto, comparison, 200);

        decision.Layout.Should().Be(ComparisonLayout.SideBySide);
        decision.WarnBeforeRendering.Should().BeFalse();
    }

    [Fact]
    public void A_terminal_too_narrow_for_the_grid_warns_then_stacks()
    {
        // Prévenir d'abord, changer de disposition ensuite : jamais de réagencement silencieux sous les yeux
        // du développeur.
        VariableComparison comparison = ManyGroups(8);

        LayoutDecision decision = LayoutSelector.Decide(ComparisonLayout.Auto, comparison, 80);

        decision.Layout.Should().Be(ComparisonLayout.Stacked);
        decision.WarnBeforeRendering.Should().BeTrue();
        decision.Reason.Should().Contain("stacked");
    }

    [Fact]
    public void There_is_no_fixed_upper_limit_on_the_number_of_groups()
    {
        // Le seuil est mesuré, jamais un nombre de groupes codé en dur. Vingt groupes tiennent dans un
        // terminal assez large, et ne tiennent pas dans un terminal étroit.
        VariableComparison comparison = ManyGroups(20);

        LayoutSelector.Decide(ComparisonLayout.Auto, comparison, 10_000)
            .Layout.Should().Be(ComparisonLayout.SideBySide);

        LayoutSelector.Decide(ComparisonLayout.Auto, comparison, 100)
            .Layout.Should().Be(ComparisonLayout.Stacked);
    }

    [Theory]
    [InlineData(ComparisonLayout.SideBySide)]
    [InlineData(ComparisonLayout.Stacked)]
    public void A_manual_layout_choice_is_honoured_at_any_group_count(ComparisonLayout requested)
    {
        // La disposition empilée reste lisible quelle que soit la largeur.
        LayoutDecision decision = LayoutSelector.Decide(requested, ManyGroups(12), 80);

        decision.Layout.Should().Be(requested);
        decision.WarnBeforeRendering.Should().BeFalse();
    }

    [Fact]
    public void The_stacked_layout_can_show_everything_even_when_the_grid_cannot()
    {
        // Toutes les variables et tous les groupes restent visibles dans la disposition empilée.
        VariableComparison comparison = ManyGroups(12);

        LayoutSelector.CanShowEverything(ComparisonLayout.SideBySide, comparison, 80)
            .Should().BeFalse();

        LayoutSelector.CanShowEverything(ComparisonLayout.Stacked, comparison, 80)
            .Should().BeTrue();
    }

    [Fact]
    public void Paging_reaches_every_row_with_none_lost_or_repeated()
    {
        // Rester navigable : les cinq cents lignes doivent toutes être atteignables.
        List<ComparisonRow> rows =
        [
            .. Enumerable.Range(1, 500).Select(index => new ComparisonRow(
                $"VAR{index:D3}", $"Var{index:D3}", [], false, false)),
        ];

        List<string> seen = [];
        int pageCount = ComparisonPager.Page(rows, 1, 20).PageCount;

        for (int page = 1; page <= pageCount; page++)
        {
            seen.AddRange(ComparisonPager.Page(rows, page, 20).Rows.Select(row => row.DisplayName));
        }

        seen.Should().HaveCount(500);
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().BeEquivalentTo(rows.Select(row => row.DisplayName));
    }

    [Fact]
    public void A_page_number_out_of_range_is_clamped_rather_than_throwing()
    {
        List<ComparisonRow> rows =
            [.. Enumerable.Range(1, 10).Select(i => new ComparisonRow($"V{i}", $"V{i}", [], false, false))];

        ComparisonPager.Page(rows, 99, 4).PageNumber.Should().Be(3);
        ComparisonPager.Page(rows, -5, 4).PageNumber.Should().Be(1);
    }

    [Fact]
    public void An_empty_comparison_pages_without_error()
    {
        ComparisonPage page = ComparisonPager.Page([], 1, 20);

        page.Rows.Should().BeEmpty();
        page.IsPaged.Should().BeFalse();
    }

    [Fact]
    public void The_page_size_never_falls_below_a_usable_minimum()
    {
        ComparisonPager.PageSizeFor(5, ComparisonLayout.SideBySide)
            .Should().BeGreaterThanOrEqualTo(ComparisonPager.MinimumPageSize);

        ComparisonPager.PageSizeFor(0, ComparisonLayout.SideBySide)
            .Should().Be(ComparisonPager.DefaultPageSize);
    }

    private static VariableComparison TwoGroups() => ComparisonBuilder.Build(
        [Snapshot("dev", ("Api__Key", "a")), Snapshot("prod", ("Api__Key", "b"))]);

    private static VariableComparison ManyGroups(int count) => ComparisonBuilder.Build(
        [.. Enumerable.Range(1, count).Select(index => Snapshot($"group-{index}", ("Api__Key", "a")))]);

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
