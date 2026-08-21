namespace DevToolbox.Presentation.Shell;

/// <summary>L'adresse du serveur confirmée par le développeur au premier démarrage.</summary>
/// <param name="BaseUrl">L'URL de base HTTPS absolue du serveur Azure DevOps.</param>
/// <param name="Collection">Le segment de collection, par exemple <c>DefaultCollection</c>.</param>
/// <remarks>
/// Il s'agit d'une adresse, pas d'un secret. Rien ici n'est confidentiel, et la saisir n'introduit aucune
/// surface de saisie d'identifiants où que ce soit dans l'outil.
/// </remarks>
public sealed record ServerConfigurationAnswer(string BaseUrl, string Collection);
