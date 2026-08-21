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
    private const string RetryLabel = "Relancer cette étape";
    private const string ContinueLabel = "Poursuivre à partir de cette étape, en gardant ce qu'elle a produit";
    private const string RestartLabel = "Recommencer depuis le début";
    private const string AbortLabel = "Abandonner";

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

        _console.WriteLine();
        _console.MarkupLine(
            $"[red]L'étape « {Markup.Escape(failed.Name)} » a échoué :[/] "
            + Markup.Escape(failed.FailureReason ?? "aucune raison n'a été consignée."));

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
