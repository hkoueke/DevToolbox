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
            grid.AddRow("[grey]serveur[/]     " + Markup.Escape(_target.Host));
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
                "[grey]DevToolbox ne demande jamais de mot de passe ni de jeton : il utilise votre session "
                + "Windows.[/]");
        }
    }

    /// <summary>Le titre et le conseil associés à une raison d'échec.</summary>
    /// <param name="reason">La raison de l'échec.</param>
    /// <returns>Un titre et une marche à suivre.</returns>
    public static (string Heading, string Advice) Describe(FailureReason reason) => reason switch
    {
        FailureReason.AuthenticationFailed => (
            "Le serveur a refusé vos identifiants Windows",
            "Vérifiez que vous êtes sur le réseau de l'entreprise et que votre compte a accès à cette "
                + "collection."),

        FailureReason.PermissionDenied => (
            "Vous n'avez pas le droit de lire ceci",
            "Demandez à l'administrateur du projet un accès en lecture à ses groupes de variables. "
                + "DevToolbox travaille strictement dans le cadre de vos propres droits et ne cherche "
                + "jamais à contourner un refus."),

        FailureReason.GroupNotFound => (
            "Ce groupe de variables n'existe plus",
            "Il a pu être renommé ou supprimé depuis la lecture de la liste. Rafraîchissez, puis "
                + "choisissez de nouveau."),

        FailureReason.ServiceUnavailable => (
            "Le serveur semble indisponible",
            "DevToolbox a cessé de réessayer plutôt que de continuer à appeler un service en échec. "
                + "Patientez un instant, puis réessayez."),

        FailureReason.ServerUnreachable => (
            "Le serveur n'a pas pu être joint",
            "C'est un problème de connectivité, et non de droits. Êtes-vous sur le réseau de "
                + "l'entreprise, ou le VPN est-il coupé ?"),

        FailureReason.ServerUntrusted => (
            "Le certificat du serveur n'a pas pu être validé",
            "DevToolbox ne désactive jamais la validation des certificats. Faites vérifier le certificat "
                + "du serveur par son administrateur."),

        FailureReason.RedirectRefused => (
            "Le serveur a répondu par une redirection, qui a été refusée",
            "Les redirections sont désactivées afin que vos identifiants Windows ne soient jamais "
                + "présentés à un autre hôte. Vérifiez que l'adresse de serveur configurée est correcte."),

        FailureReason.InvalidConfiguration => (
            "La configuration n'est pas utilisable",
            "Corrigez le réglage nommé ci-dessus, puis relancez DevToolbox."),

        FailureReason.Cancelled => (
            "Annulé",
            "Rien n'a été modifié : DevToolbox ne fait jamais que lire."),

        _ => (
            "La requête n'a pas pu aboutir",
            "Réessayez ; si cela persiste, consultez le fichier de journal de l'exécution."),
    };
}
