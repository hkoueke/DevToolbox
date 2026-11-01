using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Presentation.Prompts;
using DevToolbox.Tools.ApiDelta.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

public sealed class ApiDeltaDialoguesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apidelta-dialogues-" + Guid.NewGuid().ToString("N"));

    public ApiDeltaDialoguesTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Default_analysis_asks_only_for_relevant_files_and_explains_the_report()
    {
        using TestConsole console = StartAnalysis();
        console.Input.PushKey(ConsoleKey.Enter); // default configuration
        console.Input.PushKey(ConsoleKey.Enter); // report directory
        console.Input.PushKey(ConsoleKey.Enter); // report only
        var result = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Mode.Should().Be(RunMode.Analyse);
        result.Value.CandidatePath.Should().BeNull();
        result.Value.ApplySafeOptimizations.Should().BeFalse();
        console.Output.Should().Contain("rapport").And.Contain("facultatif").And.NotContain("mises en sourdine");
    }

    [Fact]
    public void Copy_cleanup_does_not_ask_about_a_backup_of_the_untouched_original()
    {
        using TestConsole console = StartAnalysis();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("y"); // create copy
        console.Input.PushKey(ConsoleKey.Enter); // no changes requiring manual review
        console.Input.PushKey(ConsoleKey.Enter); // keep original
        var result = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.ApplySafeOptimizations.Should().BeTrue();
        result.Value.InPlace.Should().BeFalse();
        result.Value.Backup.Should().BeTrue();
        console.Output.Should().NotContain("sauvegarde");
    }

    [Fact]
    public void Replacing_the_original_keeps_backup_and_final_confirmation_by_default()
    {
        using TestConsole console = StartAnalysis();
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("y");
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter("y"); // replace original
        console.Input.PushKey(ConsoleKey.Enter); // backup
        ApiDeltaPrompt prompt = new(console);
        var result = prompt.AskForSelection(null, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.InPlace.Should().BeTrue();
        result.Value.Backup.Should().BeTrue();
        console.Input.PushKey(ConsoleKey.Enter);
        prompt.ConfirmInPlaceWrite(result.Value.ProductionPath, "reference.backup.yaml").Should().BeFalse();
        console.Output.Should().Contain("reference.yaml").And.Contain("reference.backup.yaml");
    }

    [Fact]
    public void Found_configuration_is_still_announced_and_confirmed()
    {
        string rules = Path.Combine(_directory, "apidelta.ruleset.yaml");
        File.WriteAllText(rules, "{}");
        using TestConsole console = StartAnalysis();
        console.Input.PushKey(ConsoleKey.Enter); // use discovered rules
        console.Input.PushKey(ConsoleKey.Enter); // no custom suppression file
        console.Input.PushKey(ConsoleKey.Enter); // output directory
        console.Input.PushKey(ConsoleKey.Enter); // report only
        var result = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.RulesetPath.Should().Be(rules);
        console.Output.Should().Contain("apidelta.ruleset.yaml").And.Contain("Utiliser ces réglages");
    }

    [Fact]
    public void Cancelling_WSDL_matching_explains_that_no_comparison_will_run()
    {
        using TestConsole console = NewConsole();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        OperationCorrespondence proposed = new([new CorrespondenceEntry("Lire", "GET /clients",
            CorrespondenceOrigin.Proposed, "noms proches", [])]);
        var result = new CorrespondencePicker(console).Ask(proposed, ["GET /clients"]);
        result.IsConfirmed.Should().BeFalse();
        console.Output.Should().Contain("Comparaison annulée").And.Contain("fichiers restent inchangés")
            .And.NotContain("appariement");
    }

    [Fact]
    public async Task Clear_field_location_choices_still_map_to_the_correct_HTTP_parameter()
    {
        using TestConsole console = NewConsole();
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter); // select operation
        console.Input.PushKey(ConsoleKey.Enter); // path
        console.Input.PushKey(ConsoleKey.Enter); // method
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter); // optional field
        AuthoringSubject subject = new("Lister", [("page", "integer", false)],
            new OperationSelector("/clients", HttpOperationMethod.Get), RouteDerivationKind.Derived);
        var result = await new RouteAuthoringPrompt(console).AuthorAsync([subject], [], [], CancellationToken.None);
        result.Routes.Should().ContainSingle().Which.Placements.Should().ContainSingle()
            .Which.Location.Should().Be(ParameterLocation.Query);
        console.Output.Should().Contain("?page=2").And.NotContain("scalaire").And.NotContain("nillable");
    }

    [Theory]
    [InlineData(false, false, false, "Aucun fichier fusionné")]
    [InlineData(false, true, false, "Fichier fusionné")]
    [InlineData(true, true, false, "Votre fichier d’origine est conservé")]
    [InlineData(true, true, true, "Votre fichier d’origine a été remplacé")]
    public void Results_describe_written_files_instead_of_promising_that_nothing_changed(
        bool analysis, bool wroteDocument, bool inPlace, string expected)
    {
        using TestConsole console = NewConsole();
        RunSelection selection = new(analysis ? RunMode.Analyse : RunMode.Consolidate,
            "reference.yaml", "hash", _directory, ApplySafeOptimizations: analysis, InPlace: inPlace);
        ArtifactKind kind = analysis ? ArtifactKind.OptimizedDocument : ArtifactKind.ConsolidatedDocument;
        Artifact[] artifacts = wroteDocument
            ? [new Artifact(kind,
                Path.Combine(_directory, "resultat.yaml"), 100, "hash")]
            : [];
        new SelectionSummaryRenderer(console).RenderWritten(selection, artifacts);
        console.Output.Should().Contain(expected).And.NotContain("sha256");
        if (wroteDocument)
        {
            console.Output.Should().Contain("resultat.yaml");
        }
        else
        {
            console.Output.Should().NotContain("resultat.yaml");
        }
    }

    [Fact]
    public void A_completed_analysis_explains_skipped_comparison_without_an_internal_step_dump()
    {
        using TestConsole console = NewConsole();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ToolRun run = new(Guid.NewGuid(), "apidelta", now, ["Lecture", "Appariement des opérations"]);
        run.Steps[0].Start(now);
        run.Steps[0].Complete(now);
        run.Steps[1].Skip(now);
        run.End(RunOutcome.Success, now);
        new SelectionSummaryRenderer(console).Render(new RunSelection(RunMode.Analyse,
            "reference.yaml", "0123456789abcdef", _directory), run);
        console.Output.Should().Contain("Traitement terminé").And.Contain("un seul fichier")
            .And.NotContain("Appariement").And.NotContain("sha256");
    }

    private TestConsole StartAnalysis()
    {
        TestConsole console = NewConsole();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushTextWithEnter(Path.Combine(_directory, "reference.yaml"));
        return console;
    }

    private static TestConsole NewConsole()
    {
        TestConsole console = new();
        console.Profile.Width = 160;
        console.Interactive();
        return console;
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
