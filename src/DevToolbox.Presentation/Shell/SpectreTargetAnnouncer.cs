using DevToolbox.Application.Abstractions;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Annonce le serveur et la collection avant toute lecture.
/// </summary>
/// <remarks>
/// Le risque d'usurpation qui existe réellement ici est de lire le mauvais environnement, pas de retenir la
/// mauvaise identité : il n'y a jamais qu'une seule identité, la session Windows ambiante. Ce qui est
/// annoncé est donc la cible, sans nom d'utilisateur ni identifiant affiché, puisqu'aucun n'est détenu.
/// </remarks>
public sealed class SpectreTargetAnnouncer : ITargetAnnouncer
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée l'annonceur.</summary>
    /// <param name="console">La console où écrire.</param>
    public SpectreTargetAnnouncer(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public void AnnounceTarget(TargetAnnouncement target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow("[grey]serveur[/]", Markup.Escape(target.ServerHost));
        grid.AddRow("[grey]collection[/]", Markup.Escape(target.Collection));
        grid.AddRow("[grey]connexion[/]", Markup.Escape(TargetAnnouncement.AuthenticationMode));

        _console.Write(new Panel(grid)
            .Header("[bold]Sur le point de lire[/]")
            .Border(BoxBorder.Rounded));
    }
}
