namespace DevToolbox.Application.Abstractions;

/// <summary>Ce que l'outil annonce sur sa cible avant de lire quoi que ce soit.</summary>
/// <param name="ServerHost">L'hôte du serveur Azure DevOps configuré.</param>
/// <param name="Collection">La collection lue, par exemple <c>DefaultCollection</c>.</param>
/// <remarks>
/// Il n'y a volontairement ni identité, ni nom d'utilisateur, ni champ d'authentification. L'authentification
/// est la session Windows ambiante : il n'y a donc rien à afficher, et rien qui puisse fuir.
/// </remarks>
public sealed record TargetAnnouncement(string ServerHost, string Collection)
{
    /// <summary>Comment l'outil s'authentifie. Constant : un seul mécanisme, sans alternative.</summary>
    public static string AuthenticationMode => "integrated Windows authentication";
}
