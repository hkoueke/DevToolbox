using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Ce qu'une optimisation appliquée publie, et ce qu'elle refuse de publier.
/// </summary>
public sealed class OptimizedDocumentTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-optimise-" + Guid.NewGuid().ToString("N"));

    public OptimizedDocumentTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task An_applied_run_publishes_the_copy_and_its_manifest_and_leaves_the_original_alone()
    {
        AuditRunHarness harness = new(_root, apply: true);

        string before = AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath));

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizedDocument)).Should().BeTrue();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.Manifest)).Should().BeTrue();

        AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath))
            .Should().Be(before, "la copie est une copie : l'original n'est pas touché");
    }

    [Fact]
    public async Task The_copy_reparses_revalidates_and_says_the_same_thing()
    {
        AuditRunHarness harness = new(_root, apply: true);
        await harness.RunAsync();

        harness.Optimization.Should().NotBeNull();

        harness.Optimization.Verification.Reparsed.Should().BeTrue();
        harness.Optimization.Verification.Revalidated.Should().BeTrue();
        harness.Optimization.Verification.ReferencesResolved.Should().BeTrue();
        harness.Optimization.Verification.ContractEquivalent.Should().BeTrue();
        harness.Optimization.Verification.Differences.Should().BeEmpty();
    }

    [Fact]
    public async Task The_measured_saving_is_within_a_tenth_of_what_was_estimated()
    {
        AuditRunHarness harness = new(_root, apply: true);
        await harness.RunAsync();

        long original = new FileInfo(harness.ProductionPath).Length;
        long optimized = new FileInfo(
            Path.Combine(harness.Output, ArtifactNames.OptimizedDocument)).Length;

        long measured = original - optimized;
        long estimated = harness.Plan!.TotalEstimatedSaving;

        measured.Should().BeGreaterThan(0, "le nettoyage doit réduire le fichier");

        // Un chiffrage qui s'écarterait de plus d'un dixième de la mesure ne serait pas un chiffrage.
        Math.Abs(measured - estimated).Should().BeLessThanOrEqualTo(
            estimated / 10,
            "estimé {0} octets, mesuré {1}",
            estimated,
            measured);
    }

    [Fact]
    public async Task The_manifest_names_every_applied_transformation_and_how_to_undo_it()
    {
        AuditRunHarness harness = new(_root, apply: true);
        await harness.RunAsync();

        string manifest = await harness.ReadArtifactAsync(ArtifactNames.Manifest);

        manifest.Should().Contain("L'original n'a pas été modifié")
            .And.Contain("reversible: true")
            .And.Contain("ruleId: OPT-002")
            .And.Contain("safety: SAFE")
            .And.Contain("destination: '/components/parameters/XServiceEmetteur'")
            .And.Contain("reversal: '")
            .And.Contain("savedBytes: ");

        harness.Optimization!.Applied.Should().OnlyContain(
            transformation => transformation.Reversal.Reversible);
    }

    [Fact]
    public async Task The_manifest_measures_the_file_that_was_written_and_not_a_projection()
    {
        AuditRunHarness harness = new(_root, apply: true);
        await harness.RunAsync();

        string manifest = await harness.ReadArtifactAsync(ArtifactNames.Manifest);

        string copyPath = Path.Combine(harness.Output, ArtifactNames.OptimizedDocument);
        byte[] copy = await File.ReadAllBytesAsync(copyPath);

        manifest.Should().Contain("bytes: " + copy.Length.ToString(
            System.Globalization.CultureInfo.InvariantCulture));

        manifest.Should().Contain(AuditReportTests.Hash(copy));
    }

    [Fact]
    public async Task A_run_that_only_plans_publishes_neither_copy_nor_manifest()
    {
        AuditRunHarness harness = new(_root);
        await harness.RunAsync();

        File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizedDocument)).Should().BeFalse();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.Manifest)).Should().BeFalse();
    }

    [Fact]
    public async Task Two_applied_runs_of_the_same_file_write_the_same_copy()
    {
        AuditRunHarness first = new(_root, "premier", apply: true);
        AuditRunHarness second = new(_root, "second", apply: true);

        await first.RunAsync();
        await second.RunAsync();

        AuditReportTests.Hash(await File.ReadAllBytesAsync(
                Path.Combine(second.Output, ArtifactNames.OptimizedDocument)))
            .Should().Be(AuditReportTests.Hash(await File.ReadAllBytesAsync(
                Path.Combine(first.Output, ArtifactNames.OptimizedDocument))));
    }

    [Fact]
    public async Task The_report_says_what_was_applied_and_that_the_verification_held()
    {
        AuditRunHarness harness = new(_root, apply: true);
        await harness.RunAsync();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain("Transformations appliquées")
            .And.Contain("**Vérifiée**")
            .And.Contain("le contrat est inchangé");
    }

    [Fact]
    public async Task The_machine_readable_report_carries_the_verification_and_a_clean_outcome()
    {
        AuditRunHarness harness = new(_root, apply: true);
        await harness.RunAsync();

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"verification\"")
            .And.Contain("\"contractEquivalent\": true")
            .And.Contain("\"state\": \"applied\"")
            .And.Contain("\"code\": \"clean\"");
    }

    [Fact]
    public async Task An_in_place_run_rewrites_the_file_and_leaves_a_backup()
    {
        AuditRunHarness harness = new(_root, apply: true, inPlace: true);

        string before = AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath));

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath))
            .Should().NotBe(before, "l'écriture sur place a été demandée et confirmée");

        string backup = harness.ProductionPath + ".sauvegarde";

        File.Exists(backup).Should().BeTrue("la sauvegarde est le seul retour en arrière");
        AuditReportTests.Hash(await File.ReadAllBytesAsync(backup)).Should().Be(before);

        // Et la copie à côté n'est pas écrite : l'écriture sur place la remplace.
        File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizedDocument)).Should().BeFalse();
    }

    [Fact]
    public async Task A_verification_that_fails_publishes_nothing_reports_why_and_fails_the_run()
    {
        // Une règle saboteuse, substituée à OPT-003 : le plan la propose, elle casse le contrat, et
        // c'est la vérification sur les octets qui l'arrête. Aucune règle réelle ne permet d'éprouver
        // ce chemin, et c'est précisément le chemin qui doit tenir.
        AuditRunHarness harness = new(
            _root,
            apply: true,
            rules: [.. WriteStepFactory.Rules.Where(rule => !string.Equals(rule.RuleId, "OPT-003", StringComparison.Ordinal)), new Saboteur()]);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().NotBe(RunOutcome.Success);

        // Rien d'optimisé, et pas de manifeste : un fichier optimisé dont personne ne sait ce qui lui
        // est arrivé n'est pas un état que cet outil puisse produire.
        File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizedDocument)).Should().BeFalse();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.Manifest)).Should().BeFalse();

        // Mais les rapports sont là, et ils disent pourquoi : un refus sans explication est la pire
        // forme de refus.
        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        report.Should().Contain("**Rien n'a été publié**").And.Contain("le contrat a changé");

        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"code\": \"outputFailure\"")
            .And.Contain("\"contractEquivalent\": false");
    }

    [Fact]
    public async Task The_production_file_is_untouched_even_when_the_verification_fails()
    {
        AuditRunHarness harness = new(
            _root,
            apply: true,
            rules: [.. WriteStepFactory.Rules.Where(rule => !string.Equals(rule.RuleId, "OPT-003", StringComparison.Ordinal)), new Saboteur()]);

        string before = AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath));

        await harness.RunAsync();

        AuditReportTests.Hash(await File.ReadAllBytesAsync(harness.ProductionPath))
            .Should().Be(before);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Une règle qui retire le corps d'une réponse en prétendant extraire.
    /// </summary>
    /// <remarks>
    /// Le corps, et non la réponse entière : une opération sans réponse est refusée par la validation
    /// structurelle, et l'essai s'arrêterait à cette barrière-là. Ce qu'il faut éprouver est la
    /// dernière : une sortie qui se relit, qui est valide, dont les références se résolvent, et qui ne
    /// dit plus la même chose.
    /// </remarks>
    private sealed class Saboteur : IOptimizationRule
    {
        public string RuleId => "OPT-003";

        public bool EnabledByDefault => true;

        public bool Removes => false;

        public Result<SpecNode> Apply(SpecNode root, Transformation transformation) =>
            Result.Success(TreeEdits.Remove(root, transformation.Origins[0].Child("content")));
    }
}
