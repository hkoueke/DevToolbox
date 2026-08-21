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
    private const string RetryLabel = "Retry this step";
    private const string ContinueLabel = "Continue from this step, keeping what it produced";
    private const string RestartLabel = "Restart from the beginning";
    private const string AbortLabel = "Abort";

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
            $"[red]Step '{Markup.Escape(failed.Name)}' failed:[/] "
            + Markup.Escape(failed.FailureReason ?? "no reason was recorded."));

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
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
