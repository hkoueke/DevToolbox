using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tests.Tools.ApiDelta.Golden;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Configuration;

/// <summary>
/// Ce qu'une exécution fait d'une configuration, et ce qu'elle refuse de faire sans elle.
/// </summary>
public sealed class ConfiguredRunTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-execution-config-" + Guid.NewGuid().ToString("N"));

    public ConfiguredRunTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_run_with_no_configuration_file_succeeds_and_reports_a_clean_outcome()
    {
        // L'outil sert avant que quiconque ait écrit un jeu de règles.
        AuditRunHarness harness = new(_root);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"code\": \"clean\"");
    }

    [Fact]
    public async Task An_unjustified_suppression_beside_the_description_stops_the_run_before_any_work()
    {
        // Découverte sans être désignée, et refusée avant la lecture : aucun rapport n'est écrit, parce
        // qu'il n'y a rien eu à rapporter.
        AuditRunHarness harness = new(_root, "sourdine-nue");

        await Beside(
            harness,
            "apidelta.suppressions.yaml",
            """
            suppressions:
              - ruleId: AUD-100
                location: /paths/**
                owner: equipe
            """);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().NotBe(RunOutcome.Success);

        run.Steps[0].FailureReason.Should()
            .Contain("ne sont pas applicables").And.Contain("n'est pas justifiée");

        File.Exists(Path.Combine(harness.Output, ArtifactNames.DeltaJson))
            .Should().BeFalse("rien n'a commencé, donc rien n'a été écrit");
    }

    [Fact]
    public async Task An_invalid_ruleset_beside_the_description_stops_the_run_before_any_work()
    {
        AuditRunHarness harness = new(_root, "jeu-invalide");

        await Beside(
            harness,
            "apidelta.ruleset.yaml",
            """
            rules:
              AUD-999:
                enabled: false
            """);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().NotBe(RunOutcome.Success);

        run.Steps[0].FailureReason.Should()
            .Contain("n'est pas applicable").And.Contain("AUD-999");
    }

    [Fact]
    public async Task A_discovered_ruleset_changes_what_the_run_reports()
    {
        AuditRunHarness harness = new(_root, "jeu-valide");

        await Beside(
            harness,
            "apidelta.ruleset.yaml",
            """
            rules:
              AUD-100:
                enabled: false
            """);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        harness.Findings.Should().NotBeNull();
        harness.Findings.Findings.Should().NotContain(finding => finding.RuleId == "AUD-100");

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().NotContain("### AUD-100");
    }

    [Fact]
    public async Task A_suppression_hides_a_finding_from_the_threshold_but_lists_it_in_the_report()
    {
        AuditRunHarness harness = new(_root, "sourdine-motivee");

        await Beside(
            harness,
            "apidelta.suppressions.yaml",
            """
            suppressions:
              - ruleId: AUD-100
                location: /paths/**
                justification: Les en-tetes historiques sont figes par contrat avec le partenaire.
                owner: architecture
            """);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        harness.Findings!.Findings.Should().NotContain(finding => finding.RuleId == "AUD-100");
        harness.Findings.Suppressions.Suppressed.Should().NotBeEmpty();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain("| Règle | Emplacement | Justification | Propriétaire | Échéance |")
            .And.Contain("architecture")
            .And.Contain("figes par contrat");

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"suppressions\"")
            .And.Contain("\"state\": \"applied\"")
            .And.Contain("\"owner\": \"architecture\"");
    }

    [Fact]
    public async Task An_expired_suppression_reports_the_finding_and_the_expiry()
    {
        AuditRunHarness harness = new(_root, "sourdine-echue");

        await Beside(
            harness,
            "apidelta.suppressions.yaml",
            """
            suppressions:
              - ruleId: AUD-100
                location: /paths/**
                justification: Corrige au premier trimestre.
                owner: architecture
                expires: 2020-01-31
            """);

        await harness.RunAsync();

        harness.Findings!.Findings.Should().Contain(finding => finding.RuleId == "AUD-100");
        harness.Findings.Suppressions.Expired.Should().NotBeEmpty();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain("Échue, donc non honorée").And.Contain("2020-01-31");

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"state\": \"expired\"");
    }

    [Fact]
    public async Task A_suppression_that_matches_nothing_is_reported_as_without_object()
    {
        AuditRunHarness harness = new(_root, "sourdine-sans-objet");

        await Beside(
            harness,
            "apidelta.suppressions.yaml",
            """
            suppressions:
              - ruleId: AUD-100
                location: /paths/~1disparu/get/parameters/0
                justification: Cet emplacement a ete corrige.
                owner: architecture
            """);

        await harness.RunAsync();

        harness.Findings!.Suppressions.Unmatched.Should().NotBeEmpty();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain("Sans objet").And.Contain("n'apparie plus aucun constat");
    }

    [Fact]
    public async Task A_configured_threshold_makes_the_run_report_that_it_was_exceeded()
    {
        AuditRunHarness harness = new(_root, "seuil");

        await Beside(harness, "apidelta.ruleset.yaml", "failOn: warning");

        await harness.RunAsync();

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"code\": \"thresholdExceeded\"");
    }

    [Fact]
    public void The_outcome_code_is_not_the_process_exit_code()
    {
        // Le vocabulaire est fixé maintenant pour qu'une invocation automatisée, quand elle sera
        // spécifiée, ajoute une surface sans changer un sens. La coquille garde ses quatre codes, et
        // cette tranche n'en ajoute aucun.
        IReadOnlyList<int> shellCodes =
        [
            ExitCodes.Success,
            ExitCodes.InvalidConfiguration,
            ExitCodes.NotInteractive,
            ExitCodes.Cancelled,
        ];

        typeof(ExitCodes).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Should().HaveCount(
                shellCodes.Count, "aucun code de sortie n'a été ajouté pour cet outil");

        // Et aucun code d'issue n'a la valeur d'un code de sortie : les deux vocabulaires ne se touchent
        // pas, puisque l'un est fait de mots.
        foreach (OutcomeCode code in Enum.GetValues<OutcomeCode>())
        {
            OutcomeCodeNames.Of(code).Should().NotBeNullOrWhiteSpace();
            int.TryParse(
                    OutcomeCodeNames.Of(code),
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _)
                .Should().BeFalse();
        }
    }

    [Fact]
    public void Every_outcome_code_has_a_published_name_and_they_are_all_distinct()
    {
        Enum.GetValues<OutcomeCode>().Select(OutcomeCodeNames.Of)
            .Should().OnlyHaveUniqueItems()
            .And.Contain("outputFailure", "le contrat de sortie nomme les codes en camelCase");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Écrit un fichier de configuration à côté de la description, comme une équipe le ferait.</summary>
    private static Task Beside(AuditRunHarness harness, string fileName, string content) =>
        File.WriteAllTextAsync(
            Path.Combine(Path.GetDirectoryName(harness.ProductionPath)!, fileName), content);
}
