using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Ce que l'exécution écrit, et ce qu'elle laisse intact.
/// </summary>
public sealed class ArtifactDeterminismTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-artefacts-" + Guid.NewGuid().ToString("N"));

    public ArtifactDeterminismTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_comparison_writes_a_readable_report_and_a_machine_readable_one()
    {
        Harness harness = new(_root);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        File.Exists(Path.Combine(harness.Output, ArtifactNames.DeltaReport)).Should().BeTrue();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.DeltaJson)).Should().BeTrue();

        // Aucun fichier temporaire ne subsiste : un artefact est en place ou n'est pas.
        Directory.GetFiles(harness.Output, "*.partiel").Should().BeEmpty();
    }

    [Fact]
    public async Task The_report_names_every_section_in_order_even_when_a_section_is_empty()
    {
        Harness harness = new(_root);
        await harness.RunAsync();

        string report = await File.ReadAllTextAsync(
            Path.Combine(harness.Output, ArtifactNames.DeltaReport));

        int previous = -1;

        for (int section = 1; section <= 20; section++)
        {
            int position = report.IndexOf(
                "## " + section.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".",
                StringComparison.Ordinal);

            position.Should().BeGreaterThan(
                previous, "la section {0} doit être présente et après la précédente", section);

            previous = position;
        }
    }

    [Fact]
    public async Task Two_runs_of_the_same_comparison_write_the_same_bytes()
    {
        Harness first = new(_root, "premier");
        Harness second = new(_root, "second");

        await first.RunAsync();
        await second.RunAsync();

        foreach (string name in new[] { ArtifactNames.DeltaReport, ArtifactNames.DeltaJson })
        {
            string one = Normalise(await File.ReadAllTextAsync(Path.Combine(first.Output, name)), name);
            string other = Normalise(await File.ReadAllTextAsync(Path.Combine(second.Output, name)), name);

            other.Should().Be(one, "hors du bloc de métadonnées, {0} doit être identique", name);
        }
    }

    [Fact]
    public async Task The_production_file_is_untouched()
    {
        Harness harness = new(_root);

        string before = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(harness.ProductionPath)));

        await harness.RunAsync();

        string after = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(harness.ProductionPath)));

        after.Should().Be(before, "aucune exécution ne modifie la production sans qu'on le lui demande");
    }

    [Fact]
    public async Task A_cancelled_run_leaves_no_artifact_that_looks_finished()
    {
        Harness harness = new(_root);

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        ToolRun run = await harness.RunAsync(cancellation.Token);

        run.Outcome.Should().Be(RunOutcome.Cancelled);
        Directory.Exists(harness.Output).Should().BeFalse("rien n'a été écrit");
    }

    [Fact]
    public async Task A_correct_run_publishes_production_with_the_chosen_route_and_nothing_else()
    {
        // Le chemin que rien n'exerçait de bout en bout : choisir, énoncer, construire, vérifier, écrire.
        // Une production corrigée qui aurait changé une route non retenue se relit et se valide : seule
        // la vérification de l'étape la refuse, et seul cet essai l'exerce en vrai.
        Harness harness = new(_root, "corrigé", RunMode.Consolidate, "candidate-additive.yaml");

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(
            RunOutcome.Success,
            string.Join(
                " · ",
                run.Steps
                    .Where(step => step.FailureReason is not null)
                    .Select(step => step.Name + " : " + step.FailureReason)));

        string path = Path.Combine(harness.Output, ArtifactNames.ConsolidatedDocument);

        File.Exists(path).Should().BeTrue("la production corrigée doit avoir été écrite");

        string corrected = await File.ReadAllTextAsync(path);
        string production = await File.ReadAllTextAsync(harness.ProductionPath);

        corrected.Should().Contain(
            "x-apidelta-correction", "une correction proposée doit se déclarer comme telle");

        // Tout ce que la production disait s'y retrouve : la sélection n'a rien emporté d'autre.
        foreach (string line in production.Split('\n').Where(line => line.Trim().Length > 0))
        {
            corrected.Should().Contain(
                line.TrimEnd('\r'), "« " + line.Trim() + " » venait de la production");
        }
    }

    [Fact]
    public async Task A_correct_run_leaves_the_production_file_untouched()
    {
        Harness harness = new(_root, "intact", RunMode.Consolidate, "candidate-additive.yaml");

        string before = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(harness.ProductionPath)));

        await harness.RunAsync();

        string after = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(harness.ProductionPath)));

        after.Should().Be(before, "aucune entrée ne fait écrire par-dessus la production");
    }

    [Fact]
    public async Task Two_correct_runs_with_the_same_selection_write_the_same_bytes()
    {
        Harness first = new(_root, "premier-corrigé", RunMode.Consolidate, "candidate-additive.yaml");
        Harness second = new(_root, "second-corrigé", RunMode.Consolidate, "candidate-additive.yaml");

        await first.RunAsync();
        await second.RunAsync();

        string one = await File.ReadAllTextAsync(
            Path.Combine(first.Output, ArtifactNames.ConsolidatedDocument));
        string other = await File.ReadAllTextAsync(
            Path.Combine(second.Output, ArtifactNames.ConsolidatedDocument));

        other.Should().Be(one, "mêmes entrées et même sélection donnent les mêmes octets");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Écarte le bloc de métadonnées, et lui seul : c'est la seule part d'un artefact qui a le droit de
    /// varier d'une exécution à l'autre.
    /// </summary>
    private static string Normalise(string content, string name)
    {
        if (name.EndsWith(".json", StringComparison.Ordinal))
        {
            JsonNode parsed = JsonNode.Parse(content)!;
            parsed["runMetadata"] = "(métadonnées écartées)";

            // Les chemins vivent dans « inputs », et varient avec le dossier temporaire du test.
            parsed["inputs"] = "(chemins écartés)";

            return parsed.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        int open = content.IndexOf(MarkdownReportWriter.MetadataOpen, StringComparison.Ordinal);
        int close = content.IndexOf(MarkdownReportWriter.MetadataClose, StringComparison.Ordinal);

        string stripped = open < 0 || close < 0
            ? content
            : content.Remove(open, close - open + MarkdownReportWriter.MetadataClose.Length);

        // Le tableau des documents lus porte les noms de fichiers, qui portent le dossier du test.
        return string.Join(
            '\n',
            stripped.Split('\n').Where(line => !line.Contains("devtoolbox-artefacts-", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Monte la chaîne complète : sélection, lectures, validation, comparaison, écriture.
    /// </summary>
    private sealed class Harness
    {
        private readonly SpecReader _reader = new(NullLogger<SpecReader>.Instance);
        private readonly IClock _clock = Substitute.For<IClock>();
        private readonly IApiDeltaPrompt _prompt = Substitute.For<IApiDeltaPrompt>();
        private readonly ICorrectionSelectionPrompt _corrections =
            Substitute.For<ICorrectionSelectionPrompt>();

        internal Harness(
            string root,
            string name = "unique",
            RunMode mode = RunMode.Consolidate,
            string candidateName = "candidate-breaking.yaml")
        {
            Output = Path.Combine(root, name, "artefacts");
            ProductionPath = Write(root, name, "baseline-30.yaml");
            CandidatePath = Write(root, name, candidateName);

            _clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));

            _prompt.AskForSelection(Arg.Any<RunSelection?>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RunSelection(
                    mode, ProductionPath, string.Empty, Output, CandidatePath)));

            _corrections.Ask(Arg.Any<IReadOnlyList<CorrectionRoute>>()).Returns(
                new CorrectionSelection(
                    [new OperationSelector("/dossiers/{id}", HttpOperationMethod.Get)]));

            _corrections.Confirm(Arg.Any<CorrectionPlan>()).Returns(true);

            _prompt.ConfirmCreateDirectory(Arg.Any<string>()).Returns(true);
            _prompt.ConfirmReplace(Arg.Any<IReadOnlyList<string>>()).Returns(true);
        }

        internal string Output { get; }

        internal string ProductionPath { get; }

        internal string CandidatePath { get; }

        internal Task<ToolRun> RunAsync(CancellationToken cancellationToken = default)
        {
            OpenApiMetamodel metamodel = new();

            IReadOnlyList<IToolStep> steps =
            [
                WriteStepFactory.Select(_prompt),
                new ReadTargetStep(_reader),
                new ReadSourceStep(_reader),
                new ValidateStep(new OpenApiSemanticAdapter(), new ReferenceGraph()),
                new CompareStep(new DocumentComparer(metamodel)),
                new CorrectStep(
                    new OperationCatalog(),
                    _corrections,
                    new CorrectionBuilder(
                        new OperationCatalog(), new SplicingEmitter(new CanonicalEmitter())),
                    new CorrectionVerifier(
                        _reader, new OpenApiSemanticAdapter(), new ReferenceGraph()),
                    new DependencyResolver(metamodel),
                    ApiDeltaOptions.Default),
                WriteStepFactory.Create(_prompt, _clock),
            ];

            ToolRun run = new(
                Guid.NewGuid(),
                "apidelta",
                _clock.UtcNow,
                [.. steps.Select(step => step.Name)]);

            IRecoveryPrompt recovery = Substitute.For<IRecoveryPrompt>();
            recovery.AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>())
                .Returns(RecoveryChoice.Abort);

            SequentialStepRunner runner = new(
                _clock,
                Substitute.For<IRunCheckpointStore>(),
                [],
                recovery,
                NullLogger<SequentialStepRunner>.Instance);

            return runner.RunAsync(new ToolRunContext(run), steps, cancellationToken);
        }

        private static string Write(string root, string name, string fileName)
        {
            string directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, CorpusLoader.Read(fileName), new UTF8Encoding(false));

            return path;
        }
    }
}
