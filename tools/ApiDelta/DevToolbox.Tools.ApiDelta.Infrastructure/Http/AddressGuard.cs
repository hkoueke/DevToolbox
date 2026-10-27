using System.Net;
using System.Net.Sockets;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>
/// Décide si une adresse peut être atteinte, avant qu'aucune requête ne soit faite.
/// </summary>
/// <remarks>
/// Avant la requête, et non pendant : un contrôle qui s'exécuterait à la réception aurait déjà laissé
/// l'outil frapper à la porte d'un service interne, et le fait d'avoir frappé est en soi ce qu'on voulait
/// empêcher.
/// <para>
/// Le contrôle porte sur l'adresse <em>résolue</em>, pas sur le nom. Un nom public peut se résoudre vers
/// une adresse privée — c'est le mécanisme même par lequel on fait sortir un outil de son périmètre — et
/// comparer des chaînes de caractères n'y change rien.
/// </para>
/// <para>
/// L'interne n'est pas interdit, il est <em>confirmé en le nommant</em>. Un service derrière un pare-feu
/// d'entreprise est le cas courant ; refuser sèchement rendrait l'outil inutilisable là où il sert le
/// plus. Ce que la confirmation empêche, c'est de l'atteindre sans que personne l'ait voulu.
/// </para>
/// </remarks>
public sealed class AddressGuard
{
    private static readonly string[] Allowed = ["https", "http"];

    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;

    /// <summary>Crée le garde avec la résolution de noms du système.</summary>
    public AddressGuard()
        : this(Dns.GetHostAddressesAsync)
    {
    }

    /// <summary>
    /// Crée le garde avec une résolution de noms donnée.
    /// </summary>
    /// <param name="resolve">Comment un nom devient des adresses.</param>
    /// <remarks>
    /// Injectée pour la même raison que le temps et le système de fichiers le sont dans ce dépôt : un
    /// essai qui dépendrait du DNS de la machine éprouverait le réseau, pas la règle. Et c'est le seul
    /// moyen d'écrire l'essai qui compte vraiment — un nom public se résolvant vers une adresse privée,
    /// que l'on ne peut pas obtenir d'un vrai résolveur.
    /// </remarks>
    public AddressGuard(Func<string, CancellationToken, Task<IPAddress[]>> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);

        _resolve = resolve;
    }

    /// <summary>Contrôle une adresse.</summary>
    /// <param name="address">L'adresse à contrôler.</param>
    /// <param name="policy">Ce qu'on s'autorise à atteindre.</param>
    /// <param name="cancellationToken">Annule la résolution du nom.</param>
    /// <returns>Le refus et sa raison, ou les adresses résolues.</returns>
    public async Task<AddressCheck> CheckAsync(
        Uri address, ImportPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(policy);

        if (Scheme(address, policy) is { } refusal)
        {
            return AddressCheck.Refused(refusal);
        }

        IPAddress[] resolved;

        try
        {
            resolved = await Resolve(address, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException failure)
        {
            return AddressCheck.Refused(Refusal(
                WsdlDiagnosticCodes.AddressUnresolved,
                "« " + address.Host + " » ne se résout pas : " + failure.Message));
        }

        string[] names = [.. resolved.Select(one => one.ToString())];

        return Internal(resolved, names, policy, address) is { } internals
            ? AddressCheck.Refused(internals)
            : new AddressCheck(null, names);
    }

    /// <summary>Refuse un modèle d'adresse en le nommant.</summary>
    private static Diagnostic? Scheme(Uri address, ImportPolicy policy)
    {
        if (!Allowed.Contains(address.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return Refusal(
                WsdlDiagnosticCodes.SchemeRefused,
                "L'outil n'atteint pas une adresse en « " + address.Scheme + " ». Seul « https » est "
                    + "ouvert, « http » demandant d'être choisi explicitement. Le modèle est nommé plutôt "
                    + "que l'adresse déclarée invalide, qui ne dirait pas quoi corriger.");
        }

        if (string.Equals(address.Scheme, "http", StringComparison.OrdinalIgnoreCase)
            && !policy.AllowPlainHttp)
        {
            return Refusal(
                WsdlDiagnosticCodes.SchemeRefused,
                "« http » transporte le contrat en clair, et ce qui arrive n'est alors pas forcément ce "
                    + "qui a été envoyé. L'outil ne s'y résout que si le développeur le demande.");
        }

        return null;
    }

    private Task<IPAddress[]> Resolve(Uri address, CancellationToken cancellationToken) =>
        IPAddress.TryParse(address.Host, out IPAddress? literal)
            ? Task.FromResult<IPAddress[]>([literal])
            : _resolve(address.Host, cancellationToken);

    /// <summary>Refuse une adresse interne qui n'a pas été confirmée, en la nommant.</summary>
    private static Diagnostic? Internal(
        IPAddress[] resolved, string[] names, ImportPolicy policy, Uri address)
    {
        string[] internals = [.. names.Where((_, at) => IsInternal(resolved[at]))];

        if (internals.Length == 0)
        {
            return null;
        }

        string[] unconfirmed =
        [
            .. internals.Where(one =>
                !policy.ConfirmedAddresses.Contains(one, StringComparer.OrdinalIgnoreCase)),
        ];

        return unconfirmed.Length == 0
            ? null
            : Refusal(
                WsdlDiagnosticCodes.AddressInternal,
                "« " + address.Host + " » se résout vers " + string.Join(", ", unconfirmed)
                    + ", qui est interne. L'outil ne l'atteint pas sans une confirmation nommant cette "
                    + "adresse : un nom public peut se résoudre vers une adresse privée, et c'est "
                    + "précisément ainsi qu'on ferait sortir un outil de son périmètre.");
    }

    /// <summary>
    /// Indique qu'une adresse appartient à une plage interne.
    /// </summary>
    /// <remarks>
    /// Les plages sont énumérées plutôt que déduites. <see cref="IPAddress"/> ne répond pas à la question
    /// « est-ce privé » : il connaît la boucle locale et le lien local, et rien de plus. Les trois plages
    /// privées d'IPv4, l'espace partagé des fournisseurs et les adresses locales uniques d'IPv6 doivent
    /// donc être nommées ici, où on peut les lire.
    /// </remarks>
    private static bool IsInternal(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv4MappedToIPv6
                ? IsInternal(address.MapToIPv4())
                : (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }

        byte[] bytes = address.GetAddressBytes();

        return bytes[0] switch
        {
            10 => true,
            100 => bytes[1] >= 64 && bytes[1] <= 127,
            127 => true,
            169 => bytes[1] == 254,
            172 => bytes[1] >= 16 && bytes[1] <= 31,
            192 => bytes[1] == 168,
            _ => false,
        };
    }

    private static Diagnostic Refusal(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);
}
