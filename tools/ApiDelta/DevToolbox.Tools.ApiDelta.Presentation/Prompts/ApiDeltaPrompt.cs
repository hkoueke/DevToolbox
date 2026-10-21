using DevToolbox.Domain.Results;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>
/// Demande quels fichiers lire, ce qu'il faut en faire, et où écrire.
/// </summary>
/// <remarks>
/// Les questions sont posées dans les mots de celui qui a deux fichiers OpenAPI sous la main, non dans
/// ceux de la spécification. « Description de production » et « description candidate » sont justes et ne
/// disent rien à personne : ce qu'on a devant soi, c'est le fichier en service et le nouveau.
/// <para>
/// Le fichier en service est demandé avant le mode, et le mode décide si le nouveau est demandé du tout :
/// un examen n'a qu'un fichier, et poser la question pour l'ignorer ensuite serait une cérémonie.
/// </para>
/// </remarks>
public sealed class ApiDeltaPrompt : IApiDeltaPrompt
{
    private static readonly (RunMode Mode, string Label)[] Modes =
    [
        (RunMode.Compare, "Comparer deux fichiers : qu'est-ce que le nouveau change ?"),
        (RunMode.Correct, "Comparer, puis écrire le fichier de production avec les routes choisies"),
        (RunMode.Audit, "Examiner un seul fichier : erreurs, oublis, répétitions"),
        (RunMode.Optimize, "Chiffrer ce qu'un nettoyage ferait gagner, sans rien modifier"),
        (RunMode.Extract, "Sortir une ou plusieurs routes dans un fichier à part"),
        (RunMode.Everything, "Tout faire : comparer, examiner et chiffrer le nettoyage"),
    ];

    /// <summary>
    /// Les règles dont la sûreté ne se prouve pas, et qu'un développeur peut autoriser nommément.
    /// </summary>
    /// <remarks>
    /// Nommées ici parce que l'invite précède le plan : la question « que me laissez-vous faire » se pose
    /// avant de savoir ce qu'il y aura à faire, et c'est voulu — une autorisation donnée au vu d'une
    /// économie annoncée serait une autorisation achetée.
    /// </remarks>
    /// <summary>Le nom par convention d'un jeu de règles.</summary>
    private const string RulesetFileName = "apidelta.ruleset.yaml";

    /// <summary>Le nom par convention d'un fichier de mises en sourdine.</summary>
    private const string SuppressionsFileName = "apidelta.suppressions.yaml";

    private static readonly string[] ReviewableRules =
    [
        "OPT-006 — extraire les schémas écrits sur place (renomme des types chez les appelants)",
        "OPT-007 — retirer les composants inaccessibles (sans index des références externes)",
        "OPT-008 — remonter les en-têtes communs au niveau du chemin",
        "OPT-009 — poser le drapeau de dépréciation",
    ];

    private readonly IAnsiConsole _console;

    /// <summary>Crée l'invite.</summary>
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
            // Une exécution reprise sait déjà ce qui avait été choisi. Les fichiers seront relus et leurs
            // empreintes revérifiées de toute façon : redemander n'apprendrait rien.
            return Result.Success(resumed);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Fail<RunSelection>(FailureReason.Cancelled, "L'exécution a été annulée.");
        }

        string production = AskForPath(
            "Fichier OpenAPI (Swagger) [bold]actuellement en service[/], celui qui sert de référence");

        if (string.IsNullOrWhiteSpace(production))
        {
            return Result.Fail<RunSelection>(
                FailureReason.Cancelled, "Aucun fichier n'a été indiqué : il n'y a rien à faire.");
        }

        RunMode mode = AskForMode();

        string? candidate = null;

        if (mode is RunMode.Compare or RunMode.Correct or RunMode.Everything)
        {
            candidate = AskForPath("[bold]Nouveau[/] fichier OpenAPI, celui à comparer à l'actuel");

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return Result.Fail<RunSelection>(
                    FailureReason.Cancelled,
                    "Comparer demande deux fichiers, et le second n'a pas été indiqué.");
            }
        }

        (string? ruleset, string? suppressions) = AskForConfiguration(production);

        string output = AskForOutputDirectory(production);

        if (Outside(production, output) && !ConfirmOutside(output))
        {
            return Result.Fail<RunSelection>(
                FailureReason.Cancelled, "L'écriture hors de l'arborescence n'a pas été confirmée.");
        }

        return Result.Success(Intent(
            new RunSelection(
                mode,
                production.Trim(),
                // L'empreinte est prise par l'étape de sélection, qui seule touche les fichiers.
                string.Empty,
                output,
                CandidatePath: candidate?.Trim(),
                RulesetPath: ruleset,
                SuppressionsPath: suppressions)));
    }

    /// <inheritdoc />
    public bool ConfirmCreateDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _console.MarkupLine(
            "[yellow]Ce dossier n'existe pas encore :[/] " + Markup.Escape(directory));

        return _console.Prompt(
            new ConfirmationPrompt("Le créer et y écrire les rapports ?") { DefaultValue = true });
    }

    /// <inheritdoc />
    public bool ConfirmReplace(IReadOnlyList<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        _console.MarkupLine("[yellow]Ces rapports existent déjà et seront remplacés :[/]");

        foreach (string fileName in fileNames)
        {
            _console.MarkupLine("  [grey]" + Markup.Escape(fileName) + "[/]");
        }

        return _console.Prompt(new ConfirmationPrompt("Continuer ?") { DefaultValue = true });
    }

    /// <inheritdoc />
    /// <remarks>
    /// Trois choses sont dites avant la question, et aucune n'est décorative : le fichier, parce qu'une
    /// confirmation qui ne nomme pas sa cible ne confirme rien ; la sauvegarde, parce que c'est le seul
    /// retour en arrière possible ; et le fait que ce « oui » ne se défait pas en supprimant un rapport,
    /// parce que tous les autres « oui » de cet outil se défont ainsi.
    /// </remarks>
    public bool ConfirmInPlaceWrite(string path, string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _console.MarkupLine(
            "[yellow]Vous allez réécrire le fichier en service :[/] " + Markup.Escape(path));

        _console.MarkupLine(string.IsNullOrEmpty(backupPath)
            ? "[red]Aucune sauvegarde ne sera créée : vous l'avez refusée.[/]"
            : "[grey]L'ancien contenu sera conservé sous[/] "
                + Markup.Escape(Path.GetFileName(backupPath)));

        _console.MarkupLine(
            "[grey]C'est la seule action de cet outil qui ne se défait pas en supprimant un "
            + "rapport.[/]");

        return _console.Prompt(
            new ConfirmationPrompt("Réécrire ce fichier ?") { DefaultValue = false });
    }

    /// <summary>
    /// Demande quels fichiers de configuration employer, en annonçant ceux qui sont déjà là.
    /// </summary>
    /// <remarks>
    /// La découverte est annoncée, et non appliquée en silence. Un jeu de règles trouvé à côté de la
    /// description change ce que l'examen rapporte : qu'il soit pris sans le dire mettrait le développeur
    /// devant un rapport dont il ne saurait pas d'où viennent les gravités.
    /// <para>
    /// L'absence de fichier n'est pas un manque, et l'invite le dit : les valeurs livrées forment une
    /// configuration utilisable, et une équipe adopte un jeu de règles quand elle est en désaccord avec
    /// l'une d'elles, non pour commencer à travailler.
    /// </para>
    /// </remarks>
    private (string? Ruleset, string? Suppressions) AskForConfiguration(string production)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(production.Trim()));

        if (string.IsNullOrEmpty(directory))
        {
            return (null, null);
        }

        return (
            Beside(directory, RulesetFileName, "jeu de règles"),
            Beside(directory, SuppressionsFileName, "fichier de mises en sourdine"));
    }

    /// <summary>
    /// Le fichier trouvé à côté de la description, une fois confirmé — ou celui qu'on désigne.
    /// </summary>
    private string? Beside(string directory, string fileName, string what)
    {
        string candidate = Path.Combine(directory, fileName);

        if (File.Exists(candidate))
        {
            _console.MarkupLine(
                "[grey]Un " + what + " se trouve à côté du fichier en service :[/] "
                + Markup.Escape(fileName));

            return _console.Prompt(
                new ConfirmationPrompt("L'employer ?") { DefaultValue = true })
                ? candidate
                : null;
        }

        if (!_console.Prompt(
                new ConfirmationPrompt("Employer un " + what + " ?") { DefaultValue = false }))
        {
            return null;
        }

        string chosen = AskForPath("Chemin du " + what);

        return string.IsNullOrWhiteSpace(chosen) ? null : chosen.Trim();
    }

    /// <summary>
    /// Demande, pour un nettoyage, s'il faut seulement chiffrer ou aussi appliquer.
    /// </summary>
    /// <remarks>
    /// Chiffrer est la réponse par défaut, et le restera : un développeur qui découvre l'outil doit
    /// pouvoir voir ce qu'il ferait avant de le lui laisser faire. Appliquer produit une copie, jamais le
    /// fichier d'origine — réécrire celui-ci est une troisième question, et elle sera reposée au moment
    /// d'écrire, en nommant le fichier.
    /// <para>
    /// Les règles à relire sont autorisées par identifiant, une par une. Un « oui, tout » global
    /// reviendrait à supprimer la distinction entre ce que l'outil prouve et ce qu'il devine, qui est la
    /// seule chose que cette classe de sûreté existe pour porter.
    /// </para>
    /// </remarks>
    private RunSelection Intent(RunSelection selection)
    {
        if (selection.Mode is not (RunMode.Optimize or RunMode.Everything))
        {
            return selection;
        }

        if (!_console.Prompt(
                new ConfirmationPrompt(
                    "Appliquer à une copie ce qui est prouvé sûr, en plus de le chiffrer ?")
                { DefaultValue = false }))
        {
            return selection;
        }

        return selection with
        {
            ApplySafeOptimizations = true,
            AllowedReviewRequiredRules = AskForAllowedRules(),
            InPlace = _console.Prompt(
                new ConfirmationPrompt(
                    "Réécrire le fichier en service lui-même, au lieu d'écrire une copie ?")
                { DefaultValue = false }),
            Backup = _console.Prompt(
                new ConfirmationPrompt("Créer une sauvegarde avant d'écrire ?")
                { DefaultValue = true }),
        };
    }

    /// <summary>Les règles à relire que le développeur autorise, par identifiant.</summary>
    private IReadOnlyList<string> AskForAllowedRules()
    {
        if (!_console.Prompt(
                new ConfirmationPrompt(
                    "Autoriser aussi certaines transformations qui demandent une relecture ?")
                { DefaultValue = false }))
        {
            return [];
        }

        return
        [
            .. _console.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Lesquelles ? [grey](espace pour cocher, entrée pour valider)[/]")
                    .NotRequired()
                    .AddChoices(ReviewableRules))
                .Select(label => label[..7]),
        ];
    }

    /// <summary>Indique si le dossier de sortie est hors de l'arborescence du fichier en service.</summary>
    private static bool Outside(string production, string output)
    {
        string reference = Path.GetFullPath(
            Path.GetDirectoryName(production.Trim()) is { Length: > 0 } directory ? directory : ".");

        return !Path.GetFullPath(output.Trim())
            .StartsWith(reference, StringComparison.OrdinalIgnoreCase);
    }

    private bool ConfirmOutside(string output)
    {
        _console.MarkupLine(
            "[yellow]Ce dossier est hors de l'arborescence du fichier en service :[/] "
            + Markup.Escape(Path.GetFullPath(output.Trim())));

        return _console.Prompt(
            new ConfirmationPrompt("Y écrire quand même ?") { DefaultValue = false });
    }

    private string AskForPath(string title) =>
        _console.Prompt(
            new TextPrompt<string>(
                title + Environment.NewLine
                + "[grey]chemin du fichier · laissez vide pour revenir au menu[/]")
                .AllowEmpty());

    private RunMode AskForMode()
    {
        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Que voulez-vous faire de ce fichier ?")
                .AddChoices(Modes.Select(mode => mode.Label).ToArray()));

        return Modes.First(mode => string.Equals(mode.Label, chosen, StringComparison.Ordinal)).Mode;
    }

    /// <summary>
    /// Demande où écrire, en proposant le dossier du fichier en service.
    /// </summary>
    /// <remarks>
    /// Ce dossier est la proposition parce que c'est là que le développeur regarde, mais il reste une
    /// proposition : rien n'oblige à écrire les rapports à côté du fichier qu'ils décrivent.
    /// </remarks>
    private string AskForOutputDirectory(string production)
    {
        string suggestion = Path.GetDirectoryName(production.Trim()) ?? ".";

        if (string.IsNullOrWhiteSpace(suggestion))
        {
            suggestion = ".";
        }

        return _console.Prompt(
            new TextPrompt<string>("Dans quel dossier écrire les rapports ?")
                .DefaultValue(suggestion)
                .ShowDefaultValue());
    }
}
