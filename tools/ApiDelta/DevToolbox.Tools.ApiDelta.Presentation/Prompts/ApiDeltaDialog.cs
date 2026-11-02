using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>Des questions identifiables, une saisie distincte et des confirmations en français.</summary>
internal static class ApiDeltaDialog
{
    internal static TextPrompt<string> Text(string question) => new TextPrompt<string>(
        Question(question) + Environment.NewLine + "[grey]Votre saisie >[/]")
        .PromptStyle("green")
        .DefaultValueStyle("yellow");

    internal static SelectionPrompt<string> Choice(string question) => new SelectionPrompt<string>()
        .Title(Question(question) + Environment.NewLine + "[grey]Flèches : choisir · Entrée : valider[/]")
        .HighlightStyle("bold green");

    internal static MultiSelectionPrompt<string> Several(string question) => new MultiSelectionPrompt<string>()
        .Title(Question(question))
        .HighlightStyle("bold green")
        .InstructionsText("[grey]Espace : cocher · Entrée : valider · rien de coché : conserver les choix standards[/]");

    internal static bool Confirm(IAnsiConsole console, string question, bool defaultValue = false)
    {
        string reply = console.Prompt(Text(question + " [grey](o : oui · n : non)[/]")
            .DefaultValue(defaultValue ? "oui" : "non")
            .ShowDefaultValue()
            .Validate(value => IsYes(value) || value.Trim().ToUpperInvariant() is "N" or "NON" or "NO"
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Répondez o pour oui ou n pour non, puis appuyez sur Entrée.[/]")));
        return IsYes(reply);
    }

    internal static void Step(IAnsiConsole console, int number, string title)
    {
        console.WriteLine();
        console.MarkupLine("[bold cyan]Étape " + number.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "/3 — " + title + "[/]");
    }

    private static string Question(string text) => "[bold cyan]" + text + "[/]";

    private static bool IsYes(string value) => value.Trim().ToUpperInvariant() is "O" or "OUI" or "Y" or "YES";
}
