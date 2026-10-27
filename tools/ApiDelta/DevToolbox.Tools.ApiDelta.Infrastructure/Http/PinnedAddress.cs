using System.Net.Http;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>
/// Les adresses que le garde a validées, portées jusqu'à l'ouverture de la connexion.
/// </summary>
/// <remarks>
/// Entre le contrôle d'une adresse et la connexion, un nom d'hôte peut changer de réponse. Le garde
/// résolvait donc un jeu d'adresses, les validait, et le client résolvait le nom une seconde fois pour se
/// connecter : rien ne garantissait que la seconde réponse fût la première. Un hôte autorisé dont la
/// réponse DNS bascule vers une adresse privée atteignait ainsi un service interne que le garde n'avait
/// jamais vu — le contrôle d'adresse interne était contournable sans le toucher.
/// <para>
/// La connexion est désormais ouverte sur l'adresse <em>validée</em>, et la requête garde son nom d'hôte :
/// l'indication de nom et la validation du certificat continuent donc de porter sur le nom, et non sur
/// l'adresse. Épingler sans garder le nom aurait échangé une faille contre une autre.
/// </para>
/// <para>
/// Une requête sans adresse épinglée ne se connecte pas. C'est voulu : un second chemin vers le réseau,
/// même bien intentionné, contournerait tous les contrôles de ce dossier, et un refus bruyant est la
/// seule façon de s'en apercevoir le jour où quelqu'un l'écrit.
/// </para>
/// </remarks>
internal static class PinnedAddress
{
    /// <summary>La clé sous laquelle une requête porte ses adresses validées.</summary>
    private static readonly HttpRequestOptionsKey<IReadOnlyList<string>> Key =
        new("apidelta.pinned-addresses");

    /// <summary>Attache à une requête les adresses que le garde a validées.</summary>
    /// <param name="request">La requête.</param>
    /// <param name="resolved">Les adresses validées.</param>
    internal static void Pin(HttpRequestMessage request, IReadOnlyList<string> resolved)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolved);

        request.Options.Set(Key, resolved);
    }

    /// <summary>Les adresses épinglées d'une requête, s'il y en a.</summary>
    /// <param name="request">La requête, ou rien.</param>
    /// <returns>Les adresses, ou rien.</returns>
    internal static IReadOnlyList<string>? Of(HttpRequestMessage? request) =>
        request is not null && request.Options.TryGetValue(Key, out IReadOnlyList<string>? pinned)
            ? pinned
            : null;
}
