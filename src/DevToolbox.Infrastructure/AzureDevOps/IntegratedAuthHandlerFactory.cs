using System.Net;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Construit le gestionnaire HTTP primaire du client Azure DevOps.
/// </summary>
/// <remarks>
/// C'est là toute la conception de l'authentification. L'application ne fabrique jamais d'en-tête
/// d'autorisation, ne détient jamais de jeton et ne demande jamais d'identifiants : le gestionnaire présente
/// les identifiants Windows du processus en cours. Comme aucun type du code ne peut porter d'identifiants,
/// l'absence de toute saisie de secret est acquise par construction et non par inspection.
/// </remarks>
public static class IntegratedAuthHandlerFactory
{
    /// <summary>Crée le gestionnaire primaire.</summary>
    /// <returns>Un gestionnaire configuré pour l'authentification Windows intégrée.</returns>
    public static HttpMessageHandler Create() =>
        new HttpClientHandler
        {
            // La session Windows du développeur lui-même : c'est concrètement ce que signifie « l'appli
            // fonctionne avec la session de l'utilisateur » (Negotiate/Kerberos/NTLM).
            UseDefaultCredentials = true,

            // Évite un aller-retour de défi 401 à chaque requête.
            PreAuthenticate = true,

            // Ne jamais présenter les identifiants Windows à un hôte autre que celui qui est configuré.
            AllowAutoRedirect = false,

            // La validation du certificat reste volontairement au réglage par défaut de la plateforme et
            // n'est pas configurable : il n'existe nulle part d'option permettant de l'affaiblir.
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
}
