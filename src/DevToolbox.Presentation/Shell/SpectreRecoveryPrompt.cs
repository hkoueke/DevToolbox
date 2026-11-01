using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Propose les quatre possibilités de reprise après une étape en échec, toujours dans le même ordre.
/// </summary>
/// <remarks>
/// L'ordre est figé pour qu'un développeur l'ayant vu une fois puisse agir sans relire : relancer,
/// poursuivre, recommencer, abandonner. Rien ici ne demande d'identifiants : une étape en échec n'est
/// jamais une raison de se mettre à en réclamer.
/// </remarks>
public sealed class SpectreRecoveryPrompt : IRecoveryPrompt
{
    private const string RetryLabel = "Réessayer cette étape — garder les étapes terminées";
    private const string ContinueLabel = "Passer cette étape — continuer avec un résultat incomplet";
    private const string RestartLabel = "Tout recommencer — redemander les choix";
    private const string AbortLabel = "Arrêter ici — garder les choix déjà enregistrés";

    private readonly IAnsiConsole _console;

    /// <summary>Crée l'invite.</summary>
    /// <param name="console">La console où présenter l'invite.</param>
    public SpectreRecoveryPrompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public Task<RecoveryChoice> AskAsync(StepState failed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(failed);
        cancellationToken.ThrowIfCancellationRequested();

        _console.WriteLine();
        _console.MarkupLine(
            $"[red]L'étape « {Markup.Escape(failed.Name)} » a échoué :[/] "
            + Markup.Escape(failed.FailureReason ?? "aucune raison n'a été consignée."));

        _console.MarkupLine("[grey]Réessayer rejoue seulement l’étape en erreur. Passer l’étape ne la rejoue pas et peut laisser des données manquantes.[/]");
        _console.MarkupLine("[grey]Tout recommencer oublie les choix et résultats en mémoire, puis revient à la première question. Les fichiers et rapports déjà écrits sont conservés.[/]");
        _console.MarkupLine("[grey]Arrêter conserve le dernier enregistrement de vos choix, s’il existe ; il sera proposé à la prochaine ouverture.[/]");

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Que souhaitez-vous faire ?")
                .AddChoices(RetryLabel, ContinueLabel, RestartLabel, AbortLabel));

        RecoveryChoice choice = chosen switch
        {
            RetryLabel => RecoveryChoice.RetryStep,
            ContinueLabel => RecoveryChoice.ContinueFromStep,
            RestartLabel => RecoveryChoice.RestartAll,
            _ => RecoveryChoice.Abort,
        };

        return Task.FromResult(choice);
    }
}
