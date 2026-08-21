using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.VarCompare;

/// <summary>
/// La vue de détail : le seul endroit où une valeur est jamais affichée, et jamais pour quelque chose
/// d'illisible.
/// </summary>
public sealed class VariableDetailTests
{
    private const string ReadableValue = "https://api.example.com";

    private static readonly DateTimeOffset ReadAt = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_readable_value_is_shown_per_group()
    {
        VariableDetail detail = VariableDetail.Build(
            [Snapshot("dev", ("Api__BaseUrl", ReadableValue)), Snapshot("prod", ("Api__BaseUrl", "other"))],
            "api__baseurl");

        detail.DisplayName.Should().Be("Api__BaseUrl");
        detail.PerGroup.Should().HaveCount(2);
        detail.PerGroup[0].Value.Should().Be(ReadableValue);
        detail.PerGroup[1].Value.Should().Be("other");
    }

    [Fact]
    public void A_secret_never_yields_a_value_even_though_one_exists()
    {
        // Dire qu'une valeur existe et ne peut pas être lue. Ne jamais l'afficher, ni afficher un substitut
        // qui pourrait être pris pour elle.
        VariableGroupSnapshot dev = new(
            Summary("dev", VariableGroupOrigin.Ordinary),
            [new VariableEntry("Api__Key", "leaked-if-this-fails", IsSecret: true, IsReadOnly: false)],
            ReadAt);

        VariableDetail detail = VariableDetail.Build([dev, Snapshot("prod")], "api__key");

        detail.PerGroup[0].State.Should().Be(CellState.PresentSecret);
        detail.PerGroup[0].Value.Should().BeNull();
        detail.PerGroup[0].ExistsButUnreadable.Should().BeTrue();
    }

    [Fact]
    public void A_key_vault_backed_entry_never_yields_a_value()
    {
        VariableGroupSnapshot vault = new(
            Summary("vault", VariableGroupOrigin.KeyVaultBacked),
            [new VariableEntry("Secret", "must-not-appear", IsSecret: true, IsReadOnly: false)],
            ReadAt);

        VariableDetail detail = VariableDetail.Build([vault, Snapshot("prod")], "secret");

        detail.PerGroup[0].Value.Should().BeNull();
        detail.PerGroup[0].IsKeyVaultSourced.Should().BeTrue();
    }

    [Fact]
    public void A_degraded_group_yields_no_value_and_reports_undetermined()
    {
        VariableGroupSnapshot degraded = new(
            Summary("no-access", VariableGroupOrigin.Ordinary),
            [new VariableEntry("Api__BaseUrl", "unreadable", false, false)],
            ReadAt,
            isDegraded: true);

        VariableDetail detail = VariableDetail.Build(
            [degraded, Snapshot("prod", ("Api__BaseUrl", ReadableValue))],
            "api__baseurl");

        detail.PerGroup[0].State.Should().Be(CellState.Undetermined);
        detail.PerGroup[0].Value.Should().BeNull();
    }

    [Fact]
    public void An_absent_variable_reports_absence_with_no_value_and_no_stored_name()
    {
        VariableDetail detail = VariableDetail.Build(
            [Snapshot("dev", ("OnlyHere", "x")), Snapshot("prod")],
            "onlyhere");

        detail.PerGroup[1].State.Should().Be(CellState.Absent);
        detail.PerGroup[1].Value.Should().BeNull();
        detail.PerGroup[1].NameAsStored.Should().BeNull();
    }

    [Fact]
    public void An_empty_value_is_shown_as_empty_not_as_unreadable()
    {
        VariableDetail detail = VariableDetail.Build(
            [Snapshot("dev", ("Feature__X", "")), Snapshot("prod")],
            "feature__x");

        detail.PerGroup[0].State.Should().Be(CellState.PresentEmpty);
        detail.PerGroup[0].Value.Should().BeEmpty();
        detail.PerGroup[0].ExistsButUnreadable.Should().BeFalse();
    }

    [Fact]
    public void Each_group_keeps_its_own_spelling_of_the_name()
    {
        VariableDetail detail = VariableDetail.Build(
            [Snapshot("dev", ("API_KEY", "x")), Snapshot("prod", ("Api_Key", "x"))],
            "api_key");

        detail.PerGroup[0].NameAsStored.Should().Be("API_KEY");
        detail.PerGroup[1].NameAsStored.Should().Be("Api_Key");
    }

    private static VariableGroupSummary Summary(string name, VariableGroupOrigin origin) =>
        new(name.GetHashCode(StringComparison.Ordinal), name, null, origin, false);

    private static VariableGroupSnapshot Snapshot(
        string name,
        params (string Name, string Value)[] variables) =>
        new(
            Summary(name, VariableGroupOrigin.Ordinary),
            variables.Select(v => new VariableEntry(v.Name, v.Value, false, false)),
            ReadAt);
}
