using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.VarCompare;

/// <summary>
/// Les règles de comparaison : l'union des noms, la correspondance insensible à la casse, exactement un état
/// par cellule, et le refus de comparer moins de deux groupes.
/// </summary>
public sealed class ComparisonBuilderTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rows_are_the_union_of_names_across_all_groups()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("Shared", "a"), ("OnlyInDev", "b")),
            Snapshot("prod", ("Shared", "a"), ("OnlyInProd", "c")),
        ]);

        comparison.Rows.Select(row => row.DisplayName)
            .Should().BeEquivalentTo("OnlyInDev", "OnlyInProd", "Shared");

        comparison.Summary.TotalVariables.Should().Be(3);
    }

    [Fact]
    public void Each_distinct_name_appears_exactly_once()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("Api__Key", "x")),
            Snapshot("uat", ("Api__Key", "y")),
            Snapshot("prod", ("Api__Key", "z")),
        ]);

        comparison.Rows.Should().ContainSingle();
    }

    [Fact]
    public void Names_match_case_insensitively_and_each_group_keeps_its_own_spelling()
    {
        // La plateforme les traite comme une seule variable : inventer deux lignes inventerait une différence
        // qui n'existe pas.
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("API_KEY", "x")),
            Snapshot("prod", ("Api_Key", "x")),
        ]);

        comparison.Rows.Should().ContainSingle();

        ComparisonRow row = comparison.Rows[0];
        row.HasCasingDivergence.Should().BeTrue();
        row.Cells[0].NameAsStored.Should().Be("API_KEY");
        row.Cells[1].NameAsStored.Should().Be("Api_Key");
    }

    [Fact]
    public void Every_cell_carries_exactly_one_state_and_there_are_no_gaps()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("Present", "value"), ("Empty", string.Empty)),
            Snapshot("prod", ("Present", "value")),
        ]);

        foreach (ComparisonRow row in comparison.Rows)
        {
            row.Cells.Should().HaveCount(comparison.Groups.Count);
            row.Cells.Should().OnlyContain(cell => Enum.IsDefined(cell.State));
        }
    }

    [Theory]
    [InlineData("value", CellState.PresentWithValue)]
    [InlineData("", CellState.PresentEmpty)]
    public void A_readable_value_is_classified_by_whether_it_is_empty(string value, CellState expected)
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("Name", value)),
            Snapshot("prod"),
        ]);

        comparison.Rows[0].Cells[0].State.Should().Be(expected);
    }

    [Fact]
    public void A_variable_missing_from_a_group_is_Absent_there_and_present_elsewhere()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("OnlyHere", "x")),
            Snapshot("prod"),
        ]);

        ComparisonRow row = comparison.Rows[0];
        row.Cells[0].State.Should().Be(CellState.PresentWithValue);
        row.Cells[1].State.Should().Be(CellState.Absent);
        row.IsMissingSomewhere.Should().BeTrue();
        row.IsPresentEverywhere.Should().BeFalse();
    }

    [Fact]
    public void A_secret_is_reported_as_secret_and_never_carries_a_value()
    {
        VariableGroupSnapshot dev = new(
            Summary("dev", VariableGroupOrigin.Ordinary),
            [new VariableEntry("Api__Key", Value: null, IsSecret: true, IsReadOnly: false)],
            ReadAt);

        VariableComparison comparison = ComparisonBuilder.Build([dev, Snapshot("prod")]);

        comparison.Rows[0].Cells[0].State.Should().Be(CellState.PresentSecret);

        // Le type de cellule ne comporte aucun membre de valeur, la forme la plus forte que cette
        // vérification puisse prendre.
        typeof(ComparisonCell).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("Value");
    }

    [Fact]
    public void Every_variable_of_a_key_vault_backed_group_is_marked_key_vault_sourced()
    {
        VariableGroupSnapshot vault = new(
            Summary("vault", VariableGroupOrigin.KeyVaultBacked),
            [new VariableEntry("Secret", Value: null, IsSecret: true, IsReadOnly: false)],
            ReadAt);

        VariableComparison comparison = ComparisonBuilder.Build([vault, Snapshot("prod")]);

        comparison.Rows[0].Cells[0].IsKeyVaultSourced.Should().BeTrue();
        comparison.Rows[0].Cells[1].IsKeyVaultSourced.Should().BeFalse();
    }

    [Fact]
    public void A_degraded_group_reports_Undetermined_rather_than_looking_empty()
    {
        // Un groupe adossé à un coffre injoignable affiche ses noms avec un marqueur d'état indisponible
        // explicite, et non comme une colonne vide.
        VariableGroupSnapshot degraded = new(
            Summary("vault", VariableGroupOrigin.KeyVaultBacked),
            [new VariableEntry("Name", Value: null, IsSecret: false, IsReadOnly: false)],
            ReadAt,
            isDegraded: true);

        VariableComparison comparison = ComparisonBuilder.Build([degraded, Snapshot("prod")]);

        comparison.Rows[0].Cells[0].State.Should().Be(CellState.Undetermined);
    }

    [Fact]
    public void Differing_readable_values_are_flagged_when_present_everywhere()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("Api__BaseUrl", "https://dev.example")),
            Snapshot("prod", ("Api__BaseUrl", "https://prod.example")),
        ]);

        comparison.Rows[0].ReadableValuesDiffer.Should().BeTrue();
    }

    [Fact]
    public void Identical_readable_values_are_not_flagged_as_differing()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("Same", "x")),
            Snapshot("prod", ("Same", "x")),
        ]);

        comparison.Rows[0].ReadableValuesDiffer.Should().BeFalse();
        comparison.Rows[0].IsIdenticalEverywhere.Should().BeTrue();
    }

    [Fact]
    public void An_unreadable_value_is_never_reported_as_differing()
    {
        // Rien ne peut être conclu de la valeur d'un secret : « différence non établie » est la réponse
        // honnête.
        VariableGroupSnapshot dev = new(
            Summary("dev", VariableGroupOrigin.Ordinary),
            [new VariableEntry("Api__Key", Value: null, IsSecret: true, IsReadOnly: false)],
            ReadAt);

        VariableComparison comparison = ComparisonBuilder.Build(
            [dev, Snapshot("prod", ("Api__Key", "readable"))]);

        comparison.Rows[0].ReadableValuesDiffer.Should().BeFalse();
    }

    [Fact]
    public void A_read_only_variable_is_marked_read_only_for_that_group()
    {
        VariableGroupSnapshot dev = new(
            Summary("dev", VariableGroupOrigin.Ordinary),
            [new VariableEntry("Locked", "x", IsSecret: false, IsReadOnly: true)],
            ReadAt);

        VariableComparison comparison = ComparisonBuilder.Build(
            [dev, Snapshot("prod", ("Locked", "x"))]);

        comparison.Rows[0].Cells[0].IsReadOnly.Should().BeTrue();
        comparison.Rows[0].Cells[1].IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void A_group_with_no_variables_renders_as_an_entirely_absent_column_not_an_error()
    {
        // Cas limite : un groupe vide.
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("A", "1"), ("B", "2")),
            Snapshot("empty"),
        ]);

        comparison.Rows.Should().HaveCount(2);
        comparison.Rows.Should().OnlyContain(row => row.Cells[1].State == CellState.Absent);
    }

    [Fact]
    public void Fewer_than_two_groups_is_rejected_with_an_explanation()
    {
        Action build = () => ComparisonBuilder.Build([Snapshot("only")]);

        build.Should().Throw<ArgumentException>()
            .WithMessage("*at least two groups*");
    }

    [Fact]
    public void The_as_at_time_is_the_earliest_snapshot()
    {
        VariableGroupSnapshot early = new(Summary("dev", VariableGroupOrigin.Ordinary), [], ReadAt);
        VariableGroupSnapshot late = new(
            Summary("prod", VariableGroupOrigin.Ordinary), [], ReadAt.AddMinutes(5));

        VariableComparison comparison = ComparisonBuilder.Build([late, early]);

        comparison.RetrievedAt.Should().Be(ReadAt);
    }

    [Fact]
    public void Rows_are_sorted_predictably()
    {
        VariableComparison comparison = ComparisonBuilder.Build(
        [
            Snapshot("dev", ("zebra", "1"), ("Apple", "2"), ("mango", "3")),
            Snapshot("prod"),
        ]);

        comparison.Rows.Select(row => row.DisplayName)
            .Should().Equal("Apple", "mango", "zebra");
    }

    private static VariableGroupSummary Summary(string name, VariableGroupOrigin origin) =>
        new(name.GetHashCode(StringComparison.Ordinal), name, Description: null, origin, IsShared: false);

    private static VariableGroupSnapshot Snapshot(string name, params (string Name, string Value)[] variables) =>
        new(
            Summary(name, VariableGroupOrigin.Ordinary),
            variables.Select(variable =>
                new VariableEntry(variable.Name, variable.Value, IsSecret: false, IsReadOnly: false)),
            ReadAt);
}
