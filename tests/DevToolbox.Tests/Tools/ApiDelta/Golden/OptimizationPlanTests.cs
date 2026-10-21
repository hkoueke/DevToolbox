using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Ce que le plan de nettoyage contient, et ce qu'il ne touche pas.
/// </summary>
public sealed class OptimizationPlanTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-plan-" + Guid.NewGuid().ToString("N"));

    public OptimizationPlanTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_plan_run_writes_a_plan_and_leaves_the_file_byte_identical()
    {
        AuditRunHarness harness = new(_root);

        string before = AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath));

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizationPlan)).Should().BeTrue();

        AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath))
            .Should().Be(before, "chiffrer n'est pas nettoyer : le fichier en service n'est pas touché");

        // Et rien d'optimisé n'est publié : le plan ne mène à aucune écriture par lui-même.
        File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizedDocument)).Should().BeFalse();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.Manifest)).Should().BeFalse();
    }

    [Fact]
    public async Task The_plan_says_it_is_a_quote_and_carries_what_a_decision_needs()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        string plan = await harness.ReadArtifactAsync(ArtifactNames.OptimizationPlan);

        plan.Should().Contain("ce fichier est un devis")
            .And.Contain("state: proposed")
            .And.Contain("ruleId: OPT-002")
            .And.Contain("destination: '/components/parameters/XServiceEmetteur'")
            .And.Contain("evidence: 'sha256:")
            .And.Contain("breakEven: 2")
            .And.Contain("reversal: 'réinsérer le paramètre")
            .And.Contain("exceeded: false");

        // Groupé par sûreté : le lecteur décide d'abord ce qu'il laisse faire.
        plan.Should().Contain("safe:").And.Contain("reviewRequired:");
    }

    [Fact]
    public async Task The_plan_counts_the_repetitions_it_cannot_act_on()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        harness.Plan.Should().NotBeNull();
        harness.Plan.Uncovered.Should().NotBeEmpty(
            "le fichier porte des répétitions réelles qu'aucune règle d'optimisation ne couvre");

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain("ne sont couvertes par aucune proposition");
    }

    [Fact]
    public async Task The_report_separates_what_is_proven_safe_from_what_must_be_reviewed()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        int safe = report.IndexOf("## 14. Propositions sûres", StringComparison.Ordinal);
        int review = report.IndexOf("## 15. Propositions à revoir", StringComparison.Ordinal);

        safe.Should().BeGreaterThan(-1);
        review.Should().BeGreaterThan(safe);

        // Chaque proposition sûre nomme sa destination et ce qu'elle rapporte.
        report[safe..review].Should().Contain("**OPT-002**")
            .And.Contain("crée `/components/parameters/XServiceEmetteur`")
            .And.Contain("économie estimée")
            .And.Contain("rentable à partir de 2 occurrences");
    }

    [Fact]
    public async Task The_estimated_reduction_is_the_sum_of_the_proposals()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        harness.Plan.Should().NotBeNull();

        long expected = harness.Plan.Transformations.Sum(
            transformation => transformation.EstimatedBytesSaved);

        harness.Plan.TotalEstimatedSaving.Should().Be(expected);

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain(
            "- Économie estimée : "
            + expected.ToString(System.Globalization.CultureInfo.InvariantCulture) + " octets");
    }

    [Fact]
    public async Task Two_plans_of_the_same_file_write_the_same_bytes()
    {
        AuditRunHarness first = new(_root, "premier");
        AuditRunHarness second = new(_root, "second");

        await first.RunAsync();
        await second.RunAsync();

        string one = AuditReportTests.Normalise(
            await first.ReadArtifactAsync(ArtifactNames.OptimizationPlan));
        string other = AuditReportTests.Normalise(
            await second.ReadArtifactAsync(ArtifactNames.OptimizationPlan));

        other.Should().Be(one);
    }

    [Fact]
    public async Task The_machine_readable_report_carries_the_plan_and_the_sizes()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"optimization\"")
            .And.Contain("\"transformations\"")
            .And.Contain("\"state\": \"proposed\"")
            .And.Contain("\"OPT-002\"")
            .And.Contain("\"growthCheck\"")
            .And.Contain("\"exceeded\": false")
            .And.Contain("\"sizes\"")
            .And.Contain("\"productionBefore\"")
            .And.Contain("\"productionAfter\"")
            .And.Contain("\"kind\": \"requestBody\"");
    }

    [Fact]
    public async Task A_file_without_repetition_still_gets_a_plan_that_says_so()
    {
        // Un plan absent et un plan sans proposition ne disent pas la même chose, et seul le fichier
        // écrit permet de les distinguer après coup.
        AuditRunHarness harness = new(_root, "propre", "baseline-30.yaml");

        await harness.RunAsync();

        string plan = await harness.ReadArtifactAsync(ArtifactNames.OptimizationPlan);

        plan.Should().Contain("ce fichier est un devis");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
