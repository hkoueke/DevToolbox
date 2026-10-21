using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Ce que le rapport d'examen contient, et ce qu'il ne contient pas.
/// </summary>
public sealed class AuditReportTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-examen-rapport-" + Guid.NewGuid().ToString("N"));

    public AuditReportTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task An_audit_run_writes_an_audit_report_and_no_comparison_report()
    {
        AuditRunHarness harness = new(_root);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        File.Exists(Path.Combine(harness.Output, ArtifactNames.AuditReport)).Should().BeTrue();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.DeltaJson)).Should().BeTrue();

        // Rien de la comparaison : l'examen porte sur un seul fichier, et il n'y a pas de candidat.
        File.Exists(Path.Combine(harness.Output, ArtifactNames.DeltaReport)).Should().BeFalse();
    }

    [Fact]
    public async Task The_audit_report_carries_its_sections_in_order_and_nothing_from_the_contract_delta()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        int previous = -1;

        foreach (int section in new[] { 2, 3, 12, 13, 14, 15, 16, 17, 18, 19 })
        {
            int position = Position(report, section);

            position.Should().BeGreaterThan(
                previous, "la section {0} doit être présente et après la précédente", section);

            previous = position;
        }

        // Les changements de contrat n'entrent pas dans ce rapport : un relecteur n'a jamais les deux
        // questions en tête en même temps.
        foreach (int section in new[] { 1, 4, 5, 6, 7, 8, 9, 10, 11 })
        {
            Position(report, section).Should().Be(
                -1, "la section {0} appartient au rapport de comparaison", section);
        }
    }

    [Fact]
    public async Task The_audit_report_names_what_was_found_and_what_it_would_save()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain("### AUD-100")
            .And.Contain("14 fois à l'identique")
            .And.Contain("économie estimée")
            .And.Contain("XServiceEmetteur")
            .And.Contain("un remède automatique existe");

        // Le tableau des groupes de redondance, chiffré, avec le seuil de rentabilité.
        report.Should().Contain(
            "| Règle | Niveau | Occurrences | Taille actuelle | Taille projetée | Économie estimée "
            + "| Seuil | Sûreté | Emplacement |");
    }

    [Fact]
    public async Task Two_audits_of_the_same_file_write_the_same_bytes()
    {
        AuditRunHarness first = new(_root, "premier");
        AuditRunHarness second = new(_root, "second");

        await first.RunAsync();
        await second.RunAsync();

        string one = Normalise(await first.ReadArtifactAsync(ArtifactNames.AuditReport));
        string other = Normalise(await second.ReadArtifactAsync(ArtifactNames.AuditReport));

        other.Should().Be(one, "hors du bloc de métadonnées, le rapport doit être identique");
    }

    [Fact]
    public async Task The_machine_readable_report_carries_every_rule_outcome()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"ruleOutcomes\"")
            .And.Contain("\"findings\"")
            .And.Contain("\"redundancyGroups\"")
            .And.Contain("\"AUD-100\"")
            .And.Contain("\"safety\": \"SAFE\"")
            .And.Contain("\"profile\": \"strict\"");
    }

    [Fact]
    public async Task The_audited_file_is_untouched()
    {
        AuditRunHarness harness = new(_root);

        string before = Hash(await File.ReadAllBytesAsync(harness.ProductionPath));

        await harness.RunAsync();

        string after = Hash(await File.ReadAllBytesAsync(harness.ProductionPath));

        after.Should().Be(before, "l'examen lit, il ne modifie rien");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>L'empreinte d'un contenu, pour établir qu'il n'a pas bougé.</summary>
    internal static string Hash(byte[] content) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));

    /// <summary>Écarte le bloc de métadonnées et les chemins, seuls à varier d'une exécution à l'autre.</summary>
    internal static string Normalise(string content)
    {
        int open = content.IndexOf(MarkdownReportWriter.MetadataOpen, StringComparison.Ordinal);
        int close = content.IndexOf(MarkdownReportWriter.MetadataClose, StringComparison.Ordinal);

        string stripped = open < 0 || close < 0
            ? content
            : content.Remove(open, close - open + MarkdownReportWriter.MetadataClose.Length);

        return string.Join(
            '\n',
            stripped.Split('\n').Where(line =>
                !line.Contains("devtoolbox-", StringComparison.Ordinal)));
    }

    /// <summary>Où une section commence, ou <c>-1</c> si elle n'est pas là.</summary>
    private static int Position(string report, int section) =>
        report.IndexOf(
            "## " + section.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".",
            StringComparison.Ordinal);
}
