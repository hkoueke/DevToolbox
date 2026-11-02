using System.Text;
using System.Text.RegularExpressions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tests.Tools.ApiDelta.Golden;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Presentation.Prompts;
using FluentAssertions;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

public sealed class ApiDeltaMenu2Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "apidelta-menu2-" + Guid.NewGuid().ToString("N"));

    public ApiDeltaMenu2Tests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task The_three_results_selected_in_the_dialogue_have_the_announced_effect_on_files(int action)
    {
        using TestConsole console = Console();
        AuditRunHarness harness = new(_root, realPrompt: new ApiDeltaPrompt(console));
        byte[] original = await File.ReadAllBytesAsync(harness.ProductionPath);
        EnterSelection(console, harness.ProductionPath, action, harness.Output);
        console.Input.PushKey(ConsoleKey.Enter); // create output directory
        if (action == 2)
        {
            console.Input.PushTextWithEnter("oui"); // approve replacement at write time
        }

        ToolRun run = await harness.RunAsync();

        run.Outcome.Should().Be(RunOutcome.Success);
        File.Exists(Path.Combine(harness.Output, ArtifactNames.AuditReport)).Should().BeTrue();
        bool copy = File.Exists(Path.Combine(harness.Output, ArtifactNames.OptimizedDocument));
        copy.Should().Be(action == 1);
        byte[] after = await File.ReadAllBytesAsync(harness.ProductionPath);
        if (action == 2)
        {
            after.Should().NotEqual(original);
            (await File.ReadAllBytesAsync(harness.ProductionPath + ".sauvegarde")).Should().Equal(original);
        }
        else
        {
            after.Should().Equal(original);
            File.Exists(harness.ProductionPath + ".sauvegarde").Should().BeFalse();
        }

        console.Output.Should().Contain("Vos choix").And.Contain(harness.Output);
    }

    [Fact]
    public async Task Enter_at_the_last_confirmation_preserves_the_original_without_creating_a_backup()
    {
        using TestConsole console = Console();
        AuditRunHarness harness = new(_root, realPrompt: new ApiDeltaPrompt(console));
        byte[] original = await File.ReadAllBytesAsync(harness.ProductionPath);
        EnterSelection(console, harness.ProductionPath, 2, harness.Output);
        console.Input.PushKey(ConsoleKey.Enter); // create output directory
        console.Input.PushKey(ConsoleKey.Enter); // default: do not replace original

        await harness.RunAsync();

        (await File.ReadAllBytesAsync(harness.ProductionPath)).Should().Equal(original);
        File.Exists(harness.ProductionPath + ".sauvegarde").Should().BeFalse();
        console.Output.Should().Contain("Confirmez-vous le remplacement").And.Contain("non");
    }

    [Fact]
    public void A_missing_file_is_explained_and_a_quoted_path_with_spaces_can_be_corrected()
    {
        string path = CreateFile("fichier [essai] avec espaces.yaml");
        using TestConsole console = Console();
        Mode2(console);
        console.Input.PushTextWithEnter(Path.Combine(_root, "absent.yaml"));
        console.Input.PushTextWithEnter("\"" + path + "\"");
        console.Input.PushKey(ConsoleKey.Enter); // report only
        console.Input.PushTextWithEnter("non"); // standard settings
        console.Input.PushTextWithEnter("\"" + Path.Combine(_root, "mes rapports") + "\"");

        var selected = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);

        selected.IsSuccess.Should().BeTrue();
        selected.Value.ProductionPath.Should().Be(path);
        selected.Value.OutputDirectory.Should().Be(Path.Combine(_root, "mes rapports"));
        console.Output.Should().Contain("Ce fichier est introuvable").And.Contain("Votre saisie >");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Returning_from_the_mode_or_result_menu_cancels_without_starting_processing(bool resultMenu)
    {
        using TestConsole console = Console();
        if (resultMenu)
        {
            Mode2(console);
            console.Input.PushTextWithEnter(CreateFile("reference.yaml"));
            for (int i = 0; i < 3; i++)
            {
                console.Input.PushKey(ConsoleKey.DownArrow);
            }
        }
        else
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
        var selected = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);

        selected.IsFailure.Should().BeTrue();
        selected.Failure!.Reason.Should().Be(FailureReason.Cancelled);
        selected.Failure.Message.Should().Contain("aucun traitement");
        console.Output.Should().NotContain("Étape 3/3");
    }

    [Fact]
    public void Advanced_changes_are_never_implicitly_authorized_by_choosing_a_copy()
    {
        using TestConsole console = Console();
        Mode2(console);
        console.Input.PushTextWithEnter(CreateFile("reference.yaml"));
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter); // copy
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter); // advanced
        console.Input.PushKey(ConsoleKey.Spacebar); // only the first change
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter); // no settings file
        console.Input.PushKey(ConsoleKey.Enter); // output

        var selected = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);

        selected.IsSuccess.Should().BeTrue();
        selected.Value.AllowedReviewRequiredRules.Should().Equal("OPT-006");
        selected.Value.InPlace.Should().BeFalse();
        console.Output.Should().Contain("types générés").And.Contain("Espace : cocher");
    }

    [Fact]
    public void A_sibling_output_directory_requires_its_own_confirmation()
    {
        string folder = Path.Combine(_root, "source");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "reference.yaml");
        File.WriteAllText(file, "{}");
        using TestConsole console = Console();
        EnterSelection(console, file, 0, Path.Combine(_root, "source-autre"));
        console.Input.PushKey(ConsoleKey.Enter); // decline outside directory

        var selected = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);

        selected.IsFailure.Should().BeTrue();
        selected.Failure!.Reason.Should().Be(FailureReason.Cancelled);
        console.Output.Should().Contain("en dehors du dossier");
    }

    [Fact]
    public void Questions_and_actual_typed_input_use_different_ANSI_foreground_colors()
    {
        string file = CreateFile("reference.yaml");
        using TestConsole console = Console();
        console.SupportsAnsi(true).Colors(ColorSystem.Standard).EmitAnsiSequences();
        EnterSelection(console, file, 0, null);

        var selected = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);

        selected.IsSuccess.Should().BeTrue();
        (ForegroundAt(console.Output, "Chemin du fichier OpenAPI à vérifier").GetValueOrDefault() % 60).Should().Be(36);
        (ForegroundAt(console.Output, file).GetValueOrDefault() % 60).Should().Be(32);
    }

    [Fact]
    public void The_monochrome_dialogue_keeps_steps_input_labels_and_defaults_readable()
    {
        using TestConsole console = Console();
        console.Colors(ColorSystem.NoColors).SupportsAnsi(true);
        EnterSelection(console, CreateFile("reference.yaml"), 0, null);

        var selected = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);

        selected.IsSuccess.Should().BeTrue();
        console.Output.Should().Contain("Étape 1/3").And.Contain("Votre saisie >").And.Contain("Entrée accepte")
            .And.NotContain("\u001b[").And.NotContain("Questions en cyan");
    }

    [Fact]
    public void French_confirmations_explain_invalid_input_and_default_to_refusing_artifact_replacement()
    {
        using TestConsole console = Console();
        console.Input.PushTextWithEnter("peut-être");
        console.Input.PushTextWithEnter("oui");
        ApiDeltaPrompt prompt = new(console);
        prompt.ConfirmCreateDirectory(_root).Should().BeTrue();
        console.Input.PushKey(ConsoleKey.Enter);
        prompt.ConfirmReplace(["rapport.md"]).Should().BeFalse();
        console.Output.Should().Contain("Répondez o pour oui").And.Contain("rapport.md");
    }

    [Fact]
    public async Task Finishing_the_real_tool_consultation_returns_without_an_extra_Enter_prompt()
    {
        using TestConsole console = Console();
        AuditRunHarness harness = new(_root, realPrompt: new ApiDeltaPrompt(console));
        EnterSelection(console, harness.ProductionPath, 0, harness.Output);
        console.Input.PushKey(ConsoleKey.Enter); // create output directory
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter); // next: cleanup summary
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter); // return to OpenAPI menu

        ToolRun run = await harness.RunToolAsync(console);

        run.Outcome.Should().Be(RunOutcome.Success);
        console.Input.IsKeyAvailable().Should().BeFalse();
        console.Output.Should().Contain("Passer au bilan du nettoyage").And.Contain("Revenir au menu OpenAPI")
            .And.NotContain("Entrée : revenir au menu OpenAPI.");
    }

    [Fact]
    public void Relative_paths_are_displayed_as_full_paths_for_the_file_and_output_directory()
    {
        string file = CreateFile("reference.yaml");
        using TestConsole console = Console();
        EnterSelection(console, Path.GetRelativePath(Environment.CurrentDirectory, file), 0, null);

        var selected = new ApiDeltaPrompt(console).AskForSelection(null, CancellationToken.None);

        selected.IsSuccess.Should().BeTrue();
        selected.Value.ProductionPath.Should().Be(file);
        selected.Value.OutputDirectory.Should().Be(_root);
        console.Output.Should().Contain(_root);
    }

    [Fact]
    public void Declining_backup_is_explicitly_shown_before_the_final_replacement_confirmation()
    {
        using TestConsole console = Console();
        Mode2(console);
        console.Input.PushTextWithEnter(CreateFile("reference.yaml"));
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter); // replace original
        console.Input.PushKey(ConsoleKey.Enter); // standard cleanup
        console.Input.PushTextWithEnter("non"); // no backup
        console.Input.PushKey(ConsoleKey.Enter); // default settings
        console.Input.PushKey(ConsoleKey.Enter); // output
        ApiDeltaPrompt prompt = new(console);

        var selected = prompt.AskForSelection(null, CancellationToken.None);

        selected.IsSuccess.Should().BeTrue();
        selected.Value.Backup.Should().BeFalse();
        console.Input.PushKey(ConsoleKey.Enter); // refuse the actual replacement
        prompt.ConfirmInPlaceWrite(selected.Value.ProductionPath, string.Empty).Should().BeFalse();
        console.Output.Should().Contain("Aucune sauvegarde ne sera créée");
    }

    [Fact]
    public async Task Returning_from_the_initial_menu_exits_the_real_tool_without_an_extra_confirmation()
    {
        using TestConsole console = Console();
        AuditRunHarness harness = new(_root, realPrompt: new ApiDeltaPrompt(console));
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        ToolRun run = await harness.RunToolAsync(console);

        run.Outcome.Should().Be(RunOutcome.Cancelled);
        Directory.Exists(harness.Output).Should().BeFalse();
        console.Input.IsKeyAvailable().Should().BeFalse();
        console.Output.Should().NotContain("Entrée : revenir au menu OpenAPI.");
    }

    private static void EnterSelection(TestConsole console, string file, int action, string? output)
    {
        Mode2(console);
        console.Input.PushTextWithEnter(file);
        for (int i = 0; i < action; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
        if (action > 0)
        {
            console.Input.PushKey(ConsoleKey.Enter); // standard cleanup
        }

        if (action == 2)
        {
            console.Input.PushKey(ConsoleKey.Enter); // backup
        }

        console.Input.PushKey(ConsoleKey.Enter); // no advanced settings file
        if (output is null)
        {
            console.Input.PushKey(ConsoleKey.Enter);
        }
        else
        {
            console.Input.PushTextWithEnter(output);
        }
    }

    private static void Mode2(TestConsole console)
    {
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
    }

    private string CreateFile(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "{}");
        return path;
    }

    private static TestConsole Console()
    {
        TestConsole console = new();
        console.Profile.Width = 180;
        console.Interactive();
        return console;
    }

    private static int? ForegroundAt(string ansi, string text)
    {
        StringBuilder plain = new();
        List<int?> colors = [];
        int? color = null;
        int offset = 0;
        foreach (Match match in Regex.Matches(ansi, "\u001b\\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            string fragment = ansi[offset..match.Index];
            plain.Append(fragment);
            colors.AddRange(Enumerable.Repeat(color, fragment.Length));
            if (match.Value.EndsWith('m'))
            {
                foreach (string part in match.Value[2..^1].Split(';'))
                {
                    if (int.TryParse(part, System.Globalization.CultureInfo.InvariantCulture, out int code))
                    {
                        color = code switch { 0 or 39 => null, >= 30 and <= 37 or >= 90 and <= 97 => code, _ => color };
                    }
                }
            }

            offset = match.Index + match.Length;
        }

        plain.Append(ansi[offset..]);
        colors.AddRange(Enumerable.Repeat(color, ansi.Length - offset));
        int index = plain.ToString().IndexOf(text, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "le texte attendu doit être présent dans le vrai rendu ANSI");
        return colors[index];
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
