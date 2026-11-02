using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>Guide le choix des fichiers, du résultat et du dossier où le retrouver.</summary>
public sealed class ApiDeltaPrompt : IApiDeltaPrompt
{
    private static readonly (RunMode Mode, string Label)[] Modes =
    [
        (RunMode.Consolidate, "Comparer deux fichiers et choisir les changements à reprendre"),
        (RunMode.Analyse, "Vérifier un fichier et préparer son nettoyage"),
    ];

    private static readonly (string Id, string Label)[] ReviewableRules =
    [
        ("OPT-006", "Nommer les descriptions de données intégrées (peut changer les types générés)"),
        ("OPT-007", "Retirer les définitions sans usage connu (vérifier les autres fichiers)"),
        ("OPT-008", "Regrouper les en-têtes communs d’une même adresse"),
        ("OPT-009", "Signaler les éléments dont l’abandon est prévu"),
    ];

    private const string RulesetFileName = "apidelta.ruleset.yaml";
    private const string SuppressionsFileName = "apidelta.suppressions.yaml";
    private const string Back = "Revenir au menu OpenAPI";
    private const string ReportOnly = "Lire le rapport uniquement (conseillé pour commencer)";
    private const string CleanCopy = "Créer une copie nettoyée (original conservé)";
    private const string CleanOriginal = "Nettoyer le fichier d’origine (confirmation avant remplacement)";
    private const string Standard = "Nettoyage standard — changements vérifiés uniquement (recommandé)";
    private readonly IAnsiConsole _console;

    /// <summary>Crée le dialogue.</summary>
    /// <param name="console">La console où demander.</param>
    public ApiDeltaPrompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public Result<RunSelection> AskForSelection(RunSelection? resumed, CancellationToken cancellationToken)
    {
        if (resumed is not null)
        {
            return Result.Success(resumed);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        RunMode? mode = AskForMode();
        if (mode is null)
        {
            return Cancelled();
        }

        ExplainMode(mode.Value);
        ApiDeltaDialog.Step(_console, 1, mode is RunMode.Analyse ? "Choisir le fichier à vérifier" : "Choisir le fichier de référence");
        string production = AskForPath(mode is RunMode.Analyse
            ? "Chemin du fichier OpenAPI à vérifier (.yaml, .yml ou .json)"
            : "Chemin de votre fichier OpenAPI de référence (.yaml, .yml ou .json)");
        if (string.IsNullOrWhiteSpace(production))
        {
            return Cancelled();
        }

        Companion? companion = AskForCompanion(mode.Value);
        if (companion is not null && string.IsNullOrWhiteSpace(companion.Path))
        {
            return Cancelled();
        }

        RunSelection selection = new(mode.Value, production, string.Empty,
            Path.GetDirectoryName(production) ?? ".", CandidatePath: companion?.Path);
        Result<RunSelection> intent = AskForIntent(selection);
        if (intent.IsFailure)
        {
            return intent;
        }

        return Complete(intent.Value);
    }

    private Result<RunSelection> Complete(RunSelection selection)
    {
        (string? ruleset, string? suppressions) = AskForConfiguration(selection.ProductionPath);
        ApiDeltaDialog.Step(_console, 3, "Choisir où retrouver les résultats");
        string output = AskForOutputDirectory(selection);
        if (Outside(selection.ProductionPath, output) && !ConfirmOutside(output))
        {
            return Cancelled();
        }

        selection = selection with { OutputDirectory = output, RulesetPath = ruleset, SuppressionsPath = suppressions };
        Summarize(selection);
        return Result.Success(selection);
    }

    private RunMode? AskForMode()
    {
        string chosen = _console.Prompt(ApiDeltaDialog.Choice("Que voulez-vous faire ?")
            .AddChoices([.. Modes.Select(mode => mode.Label), Back]));
        return string.Equals(chosen, Back, StringComparison.Ordinal) ? null
            : Modes.First(mode => string.Equals(mode.Label, chosen, StringComparison.Ordinal)).Mode;
    }

    private void ExplainMode(RunMode mode)
    {
        _console.MarkupLine(mode is RunMode.Analyse
            ? "[grey]Ce parcours vérifie un seul fichier OpenAPI. Il produit un rapport et peut préparer un fichier nettoyé.[/]"
            : "[grey]Choisissez votre fichier de référence, puis le nouveau fichier. Vous choisirez ensuite les changements à reprendre dans une copie.[/]");
        _console.MarkupLine(_console.Profile.Capabilities.ColorSystem is ColorSystem.NoColors
            ? "[grey]Répondez après « Votre saisie > ». Entrée accepte la valeur proposée entre parenthèses.[/]"
            : "[grey]Questions en cyan · votre saisie en vert · valeur proposée en jaune. Entrée accepte la valeur proposée.[/]");
    }

    private Result<RunSelection> AskForIntent(RunSelection selection)
    {
        if (selection.Mode is not RunMode.Analyse)
        {
            return Result.Success(selection);
        }

        ApiDeltaDialog.Step(_console, 2, "Choisir le résultat souhaité");
        _console.MarkupLine("[grey]Le nettoyage standard regroupe les répétitions vérifiées. Le rapport détaille aussi les points à corriger manuellement.[/]");
        string action = _console.Prompt(ApiDeltaDialog.Choice("Que voulez-vous obtenir pour ce fichier ?")
            .AddChoices(ReportOnly, CleanCopy, CleanOriginal, Back));
        if (string.Equals(action, Back, StringComparison.Ordinal))
        {
            return Cancelled();
        }

        if (string.Equals(action, ReportOnly, StringComparison.Ordinal))
        {
            _console.MarkupLine("[grey]Un rapport sera créé. Votre fichier OpenAPI restera inchangé.[/]");
            return Result.Success(selection);
        }

        IReadOnlyList<string> allowed = AskForAllowedRules();
        bool inPlace = string.Equals(action, CleanOriginal, StringComparison.Ordinal);
        bool backup = !inPlace || ApiDeltaDialog.Confirm(_console,
            "Conserver une sauvegarde avant de remplacer l’original ?", defaultValue: true);
        _console.MarkupLine(inPlace
            ? "[yellow]Le remplacement de l’original vous sera confirmé juste avant l’écriture, avec son chemin.[/]"
            : "[grey]La copie nettoyée sera créée dans le dossier des résultats. Votre original restera inchangé.[/]");
        return Result.Success(selection with
        {
            ApplySafeOptimizations = true,
            AllowedReviewRequiredRules = allowed,
            InPlace = inPlace,
            Backup = backup,
        });
    }

    private IReadOnlyList<string> AskForAllowedRules()
    {
        string level = _console.Prompt(ApiDeltaDialog.Choice("Quel niveau de nettoyage souhaitez-vous ?")
            .AddChoices(Standard, "Choisir des changements avancés à relire"));
        if (string.Equals(level, Standard, StringComparison.Ordinal))
        {
            return [];
        }

        _console.MarkupLine("[yellow]Ces changements peuvent modifier les types générés ou les usages dans d’autres fichiers. Cochez uniquement ceux que vous voulez examiner.[/]");
        return [.. _console.Prompt(ApiDeltaDialog.Several("Quels changements avancés autorisez-vous ?")
            .NotRequired().AddChoices(ReviewableRules.Select(rule => rule.Label)))
            .Select(label => ReviewableRules.First(rule => string.Equals(rule.Label, label, StringComparison.Ordinal)).Id)];
    }

    private string AskForPath(string title)
    {
        _console.MarkupLine("[grey]Collez le chemin complet du fichier. Les guillemets autour du chemin sont acceptés. Laissez vide pour revenir au menu.[/]");
        string chosen = NormalizePath(_console.Prompt(ApiDeltaDialog.Text(title).AllowEmpty()
            .Validate(value => string.IsNullOrWhiteSpace(value) || File.Exists(NormalizePath(value))
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Ce fichier est introuvable. Indiquez son chemin complet, ou laissez vide pour revenir.[/]"))));
        return string.IsNullOrWhiteSpace(chosen) ? string.Empty : Path.GetFullPath(chosen);
    }

    private Companion? AskForCompanion(RunMode mode)
    {
        if (mode is not RunMode.Consolidate)
        {
            return null;
        }

        ApiDeltaDialog.Step(_console, 2, "Choisir le nouveau fichier");
        return new Companion(AskForPath("Le fichier à comparer (.yaml, .yml, .json ou .wsdl)"));
    }

    private string AskForOutputDirectory(RunSelection selection)
    {
        string suggestion = Path.GetDirectoryName(selection.ProductionPath) ?? ".";
        _console.MarkupLine(selection.ApplySafeOptimizations && !selection.InPlace
            ? "[grey]Vous y trouverez les rapports et le fichier nettoyé. L’original est conservé.[/]"
            : "[grey]Vous y trouverez les rapports de vérification et les propositions de nettoyage.[/]");
        _console.MarkupLine("[grey]Entrée conserve le dossier proposé, à côté du fichier choisi.[/]");
        return Path.GetFullPath(NormalizePath(_console.Prompt(ApiDeltaDialog.Text("Dossier dans lequel retrouver les résultats")
            .DefaultValue(suggestion).ShowDefaultValue()
            .Validate(value => ValidDirectory(value) ? ValidationResult.Success()
                : ValidationResult.Error("[red]Ce chemin de dossier est invalide. Indiquez un dossier complet.[/]")))));
    }

    private (string? Ruleset, string? Suppressions) AskForConfiguration(string production)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(production))!;
        bool found = File.Exists(Path.Combine(directory, RulesetFileName)) || File.Exists(Path.Combine(directory, SuppressionsFileName));
        if (!found)
        {
            _console.MarkupLine("[grey]Les réglages standards conviennent pour commencer. Les fichiers de réglages avancés sont facultatifs.[/]");
            if (!ApiDeltaDialog.Confirm(_console, "Voulez-vous choisir des fichiers de réglages avancés ?"))
            {
                return (null, null);
            }
        }

        return (Beside(directory, RulesetFileName, "règles de vérification", "choisit les vérifications et leur importance"),
            Beside(directory, SuppressionsFileName, "exceptions aux vérifications", "masque des points déjà examinés"));
    }

    private string? Beside(string directory, string fileName, string what, string does)
    {
        string candidate = Path.Combine(directory, fileName);
        if (File.Exists(candidate))
        {
            _console.MarkupLine("[grey]Trouvé à côté de votre fichier :[/] [bold]" + Markup.Escape(fileName)
                + "[/] [grey](" + what + " : " + does + ")[/]");
            return ApiDeltaDialog.Confirm(_console, "Utiliser ces réglages ?", defaultValue: true) ? candidate : null;
        }

        _console.MarkupLine("[grey]" + char.ToUpperInvariant(what[0]) + what[1..] + " : " + does
            + ". Vous pouvez garder les réglages standards.[/]");
        if (!ApiDeltaDialog.Confirm(_console, "Choisir un fichier pour ces réglages ?"))
        {
            return null;
        }

        string chosen = AskForPath("Chemin du fichier de réglages");
        return string.IsNullOrWhiteSpace(chosen) ? null : chosen;
    }

    /// <inheritdoc />
    public bool ConfirmCreateDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _console.MarkupLine("[yellow]Ce dossier n’existe pas encore :[/] " + Markup.Escape(directory));
        return ApiDeltaDialog.Confirm(_console, "Créer ce dossier et y écrire les résultats ?", defaultValue: true);
    }

    /// <inheritdoc />
    public bool ConfirmReplace(IReadOnlyList<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        _console.MarkupLine("[yellow]Ces fichiers de résultat existent déjà et seront remplacés :[/]");
        foreach (string fileName in fileNames)
        {
            _console.MarkupLine("  " + Markup.Escape(fileName));
        }

        return ApiDeltaDialog.Confirm(_console, "Remplacer les fichiers de résultat listés ci-dessus ?");
    }

    /// <inheritdoc />
    public bool ConfirmInPlaceWrite(string path, string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _console.MarkupLine("[yellow]Vous allez remplacer ce fichier :[/] " + Markup.Escape(path));
        _console.MarkupLine(string.IsNullOrEmpty(backupPath)
            ? "[red]Aucune sauvegarde ne sera créée : vous l’avez refusée.[/]"
            : "[grey]L’ancien contenu sera conservé sous[/] " + Markup.Escape(Path.GetFileName(backupPath)));
        return ApiDeltaDialog.Confirm(_console, "Confirmez-vous le remplacement de ce fichier d’origine ?");
    }

    private bool ConfirmOutside(string output)
    {
        _console.MarkupLine("[yellow]Ce dossier est en dehors du dossier du fichier choisi :[/] " + Markup.Escape(Path.GetFullPath(output)));
        return ApiDeltaDialog.Confirm(_console, "Écrire les résultats dans ce dossier ?");
    }

    private void Summarize(RunSelection selection)
    {
        string action = "Comparaison et sélection des changements";
        if (selection.Mode is RunMode.Analyse)
        {
            action = selection.ApplySafeOptimizations ? "Création d’une copie nettoyée" : "Rapport uniquement";
        }
        if (selection.InPlace)
        {
            action = "Nettoyage de l’original, avec confirmation avant écriture";
        }

        _console.MarkupLine("[bold cyan]Vos choix[/]");
        _console.MarkupLine("Fichier : [green]" + Markup.Escape(selection.ProductionPath) + "[/]");
        _console.MarkupLine("Résultat : [green]" + action + "[/]");
        _console.MarkupLine("Dossier : [green]" + Markup.Escape(selection.OutputDirectory) + "[/]");
    }

    private static bool Outside(string production, string output)
    {
        string reference = Path.GetDirectoryName(Path.GetFullPath(production))!;
        string relative = Path.GetRelativePath(reference, Path.GetFullPath(output));
        return Path.IsPathRooted(relative) || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string NormalizePath(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] is '"' && trimmed[^1] is '"' ? trimmed[1..^1] : trimmed;
    }

    private static bool ValidDirectory(string value)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(value) && !File.Exists(Path.GetFullPath(NormalizePath(value)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static Result<RunSelection> Cancelled() => Result.Fail<RunSelection>(
        FailureReason.Cancelled, "Retour au menu : aucun traitement n’a été lancé.");

    private sealed record Companion(string Path);
}
