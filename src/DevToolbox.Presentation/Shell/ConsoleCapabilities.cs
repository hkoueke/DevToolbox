using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Interroge ce que le terminal sait réellement faire, afin que le rendu se dégrade correctement au lieu de
/// présumer la couleur, l'Unicode ou l'interactivité.
/// </summary>
public sealed class ConsoleCapabilities
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée la sonde de capacités.</summary>
    /// <param name="console">La console à interroger.</param>
    public ConsoleCapabilities(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Indique si le terminal peut accepter une saisie interactive.</summary>
    public bool IsInteractive => _console.Profile.Capabilities.Interactive;

    /// <summary>Indique si le terminal restitue la couleur.</summary>
    public bool SupportsColour => _console.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;

    /// <summary>Indique si le terminal restitue l'Unicode, par opposition à l'ASCII seul.</summary>
    public bool SupportsUnicode => _console.Profile.Capabilities.Unicode;

    /// <summary>La largeur utile, qui départage la disposition en colonnes et la disposition empilée.</summary>
    public int Width => _console.Profile.Width;

    /// <summary>La hauteur utile, qui détermine la taille d'une page de comparaison.</summary>
    public int Height => _console.Profile.Height;

    /// <summary>
    /// Explique que la session ne peut pas piloter un menu, et renvoie le code de sortie documenté. Ne reste
    /// jamais bloqué sur une invite à laquelle personne ne peut répondre.
    /// </summary>
    /// <returns>Le code de sortie que le processus doit renvoyer.</returns>
    public int ReportNonInteractiveAndExit()
    {
        _console.MarkupLine(
            "[yellow]DevToolbox est un outil interactif, et cette session ne peut pas recevoir de saisie.[/]");
        _console.MarkupLine("Lancez-le depuis un terminal qui accepte les invites interactives.");

        return ExitCodes.NotInteractive;
    }
}
