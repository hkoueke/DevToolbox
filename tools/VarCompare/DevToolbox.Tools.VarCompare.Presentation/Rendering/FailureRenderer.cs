using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Explique chaque échec que la passerelle peut produire, dans des termes sur lesquels un développeur peut
/// agir.
/// </summary>
/// <remarks>
/// Deux règles gouvernent tout ce qui suit. D'abord, un serveur injoignable et un refus d'accès ne doivent
/// jamais se lire pareil : ils appellent des réactions entièrement différentes. Ensuite, rien ne fuit :
/// aucun code de statut, aucune URL, aucun corps de réponse, aucune trace d'appel.
/// </remarks>
public sealed class FailureRenderer
{
    private readonly IAnsiConsole _console;
    private readonly ServerTarget _target;

    /// <summary>Crée le moteur de rendu.</summary>
    /// <param name="console">La console où écrire.</param>
    /// <param name="target">La cible de la boîte à outils, nommée dans le message d'authentification.</param>
    public FailureRenderer(IAnsiConsole console, ServerTarget target)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(target);

        _console = console;
        _target = target;
    }

    /// <summary>Affiche un échec avec un titre, une explication et la marche à suivre.</summary>
    /// <param name="failure">L'échec à expliquer.</param>
    public void Render(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        (string heading, string advice) = Describe(failure.Reason);

        Grid grid = new();
        grid.AddColumn();

        grid.AddRow("[grey]" + Markup.Escape(failure.Message) + "[/]");

        if (failure.Reason == FailureReason.AuthenticationFailed)
        {
            grid.AddRow(string.Empty);
            grid.AddRow("[grey]server[/]      " + Markup.Escape(_target.Host));
            grid.AddRow("[grey]collection[/]  " + Markup.Escape(_target.Collection));
        }

        grid.AddRow(string.Empty);
        grid.AddRow("[grey]" + Markup.Escape(advice) + "[/]");

        _console.Write(new Panel(grid)
            .Header($"[red]{Markup.Escape(heading)}[/]")
            .Border(BoxBorder.Rounded));

        if (failure.Reason == FailureReason.AuthenticationFailed)
        {
            // Aucune demande d'identifiants ne suit. Jamais.
            _console.MarkupLine(
                "[grey]DevToolbox never asks for a password or a token — it uses your Windows session.[/]");
        }
    }

    /// <summary>Le titre et le conseil associés à une raison d'échec.</summary>
    /// <param name="reason">La raison de l'échec.</param>
    /// <returns>Un titre et une marche à suivre.</returns>
    public static (string Heading, string Advice) Describe(FailureReason reason) => reason switch
    {
        FailureReason.AuthenticationFailed => (
            "The server rejected your Windows credentials",
            "Check that you are on the corporate network and that your account has access to this "
                + "collection."),

        FailureReason.PermissionDenied => (
            "You do not have permission to read this",
            "Ask whoever administers the project to grant you read access to its variable groups. "
                + "DevToolbox works strictly within your own rights and never tries to work around a denial."),

        FailureReason.GroupNotFound => (
            "That variable group no longer exists",
            "It may have been renamed or deleted since the list was read. Refresh and choose again."),

        FailureReason.ServiceUnavailable => (
            "The server appears to be unavailable",
            "DevToolbox stopped retrying rather than continuing to call a failing service. "
                + "Wait a moment and try again."),

        FailureReason.ServerUnreachable => (
            "The server could not be reached",
            "This is a connectivity problem, not a permissions one. Are you on the corporate network, "
                + "or is the VPN down?"),

        FailureReason.ServerUntrusted => (
            "The server's certificate could not be validated",
            "DevToolbox never disables certificate validation. Check the server's certificate with "
                + "whoever administers it."),

        FailureReason.RedirectRefused => (
            "The server answered with a redirect, which was refused",
            "Redirects are disabled so your Windows credentials are never presented to another host. "
                + "Check that the configured server address is correct."),

        FailureReason.InvalidConfiguration => (
            "The configuration is not usable",
            "Correct the setting named above and start DevToolbox again."),

        FailureReason.Cancelled => (
            "Cancelled",
            "Nothing was changed — DevToolbox only ever reads."),

        _ => (
            "The request could not be completed",
            "Try again; if it keeps happening, check the log file for the run."),
    };
}
