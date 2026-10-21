using System.Text;
using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Extraction;

/// <summary>
/// Ce qu'une exécution en mode extraction écrit, et ce qu'elle laisse intact.
/// </summary>
public sealed class ExtractRunTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-extrait-execution-" + Guid.NewGuid().ToString("N"));

    public ExtractRunTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task An_extract_run_writes_the_extract_its_report_and_nothing_else()
    {
        Harness harness = new(_root);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);

        File.Exists(Path.Combine(harness.Output, ArtifactNames.Extract)).Should().BeTrue();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.ExtractReport)).Should().BeTrue();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.DeltaJson)).Should().BeTrue();

        // Rien de la comparaison ni de l'examen : ce mode ne compare rien et n'examine rien.
        File.Exists(Path.Combine(harness.Output, ArtifactNames.DeltaReport)).Should().BeFalse();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.AuditReport)).Should().BeFalse();
        File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizationPlan)).Should().BeFalse();
    }

    [Fact]
    public async Task The_source_is_byte_identical_and_there_is_no_in_place_path()
    {
        Harness harness = new(_root, inPlace: true);

        string before = Hash(await File.ReadAllBytesAsync(harness.SourcePath));

        await harness.RunAsync();

        Hash(await File.ReadAllBytesAsync(harness.SourcePath)).Should().Be(
            before, "il n'existe aucun chemin d'ici vers la réécriture de la source");
    }

    [Fact]
    public async Task The_report_names_what_each_operation_inherited_and_the_verdict()
    {
        Harness harness = new(_root);
        await harness.RunAsync();

        string report = await harness.ReadAsync(ArtifactNames.ExtractReport);

        report.Should().Contain("## 20. Extrait")
            .And.Contain("| Opération | Paramètres hérités | Serveurs | Sécurité |")
            .And.Contain("GET /documents")
            .And.Contain("X-serviceEmetteur")
            .And.Contain("bearerAuth")
            .And.Contain("**Vérifié**");
    }

    [Fact]
    public async Task The_machine_readable_report_carries_the_extract()
    {
        Harness harness = new(_root);
        await harness.RunAsync();

        string json = await harness.ReadAsync(ArtifactNames.DeltaJson);

        json.Should().Contain("\"extract\"")
            .And.Contain("\"pathParameters\"")
            .And.Contain("\"X-serviceEmetteur\"")
            .And.Contain("\"carriedTags\"")
            .And.Contain("\"operationsEquivalent\": true")
            .And.Contain("\"code\": \"clean\"");
    }

    [Fact]
    public async Task An_empty_selection_stops_the_run_rather_than_writing_an_empty_extract()
    {
        Harness harness = new(_root, selectors: []);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().NotBe(RunOutcome.Success);

        File.Exists(Path.Combine(harness.Output, ArtifactNames.Extract)).Should().BeFalse();
    }

    [Fact]
    public async Task A_resumed_selection_naming_a_vanished_operation_is_refused_by_name()
    {
        // Reprendre relit : une opération disparue depuis doit être nommée, non comptée.
        Harness harness = new(
            _root,
            selectors: [new OperationSelector("/disparu", HttpOperationMethod.Get)]);

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().NotBe(RunOutcome.Success);

        run.Steps.First(step => step.Status == StepStatus.Failed).FailureReason
            .Should().Contain("GET /disparu");
    }

    [Fact]
    public async Task Two_extract_runs_of_the_same_selection_write_the_same_bytes()
    {
        // Le même fichier et la même sélection, deux fois. La marque de provenance porte le chemin de la
        // source : deux extraits de deux copies rangées ailleurs diffèrent donc légitimement, et c'est
        // la répétition sur la même source qui doit être identique.
        Harness harness = new(_root);

        await harness.RunAsync();

        string first = Hash(
            await File.ReadAllBytesAsync(Path.Combine(harness.Output, ArtifactNames.Extract)));

        await harness.RunAsync();

        Hash(await File.ReadAllBytesAsync(Path.Combine(harness.Output, ArtifactNames.Extract)))
            .Should().Be(first);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Hash(byte[] content) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));

    /// <summary>Monte la chaîne d'une extraction : sélection, lecture, validation, extraction, écriture.</summary>
    private sealed class Harness
    {
        private readonly IClock _clock = Substitute.For<IClock>();
        private readonly IApiDeltaPrompt _prompt = Substitute.For<IApiDeltaPrompt>();
        private readonly IOperationSelectionPrompt _picker =
            Substitute.For<IOperationSelectionPrompt>();

        internal Harness(
            string root,
            string name = "unique",
            IReadOnlyList<OperationSelector>? selectors = null,
            bool inPlace = false)
        {
            Output = Path.Combine(root, name, "artefacts");
            SourcePath = Write(root, name);

            _clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

            _prompt.AskForSelection(Arg.Any<RunSelection?>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RunSelection(
                    RunMode.Extract, SourcePath, string.Empty, Output, InPlace: inPlace)));

            _prompt.ConfirmCreateDirectory(Arg.Any<string>()).Returns(true);
            _prompt.ConfirmReplace(Arg.Any<IReadOnlyList<string>>()).Returns(true);
            _prompt.ConfirmInPlaceWrite(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            _picker.Ask(Arg.Any<SpecDocument>()).Returns(
                selectors ?? [new OperationSelector("/documents", HttpOperationMethod.Get)]);

            _picker.Confirm(Arg.Any<IReadOnlyList<OperationSelector>>(), Arg.Any<int>())
                .Returns(true);
        }

        internal string Output { get; }

        internal string SourcePath { get; }

        internal Task<string> ReadAsync(string fileName) =>
            File.ReadAllTextAsync(Path.Combine(Output, fileName));

        internal Task<ToolRun> RunAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IToolStep> steps =
            [
                Golden.WriteStepFactory.Select(_prompt),
                new ReadTargetStep(new SpecReader(NullLogger<SpecReader>.Instance)),
                new ValidateStep(new OpenApiSemanticAdapter(), new ReferenceGraph()),
                new ExtractStep(
                    ExtractHarness.Builder(),
                    new CanonicalEmitter(),
                    ExtractHarness.Verifier(),
                    _picker,
                    ApiDeltaOptions.Default,
                    _clock),
                Golden.WriteStepFactory.Create(_prompt, _clock),
            ];

            ToolRun run = new(
                Guid.NewGuid(), "apidelta", _clock.UtcNow, [.. steps.Select(step => step.Name)]);

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

        private static string Write(string root, string name)
        {
            string directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, "baseline-30.yaml");
            File.WriteAllText(path, CorpusLoader.Read("baseline-30.yaml"), new UTF8Encoding(false));

            return path;
        }
    }
}
