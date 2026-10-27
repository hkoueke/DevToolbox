using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>Configure le client typé qui récupère un contrat par son adresse.</summary>
/// <remarks>
/// Dans cet espace de noms, et non auprès des autres enregistrements, parce que la règle que
/// <c>LayeringTests</c> tient demande que rien hors de <c>Infrastructure.Http</c> ne dépende de
/// <c>System.Net.Http</c> — la configuration d’un client HTTP en dépend autant que son usage.
/// Le résultat est accessoirement meilleur : la configuration vit à côté du code qu’elle configure.
/// </remarks>
internal static class HttpServices
{
    /// <summary>Enregistre le récupérateur et son client.</summary>
    /// <param name="services">La collection de services.</param>
    internal static void Add(IServiceCollection services)
    {
        // Le client est typé et vient de la fabrique : aucun « new HttpClient() » n'est permis dans ce
        // dépôt, et la raison en est pratique autant que doctrinale — un client construit à la main
        // n'hérite ni du gestionnaire de résilience, ni de la durée de vie qui évite l'épuisement des
        // connexions.
        //
        // Le suivi automatique des redirections est désactivé, et c'est la seule décision de cette
        // méthode qui mérite une phrase : chaque saut doit repasser par le garde d'adresses. Une
        // redirection menant d'une adresse publique vers un service interne passerait inaperçue si la
        // bibliothèque la suivait pour nous.
        _ = services.AddHttpClient<IContractFetcher, ContractFetcher>(client =>
            client.DefaultRequestHeaders.Accept.ParseAdd("text/xml, application/xml, */*"))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,

                // Aucun identifiant n'est acquis ni envoyé : une fonctionnalité qui n'en détient aucun ne
                // peut pas en divulguer. Les identifiants par défaut de la machine en sont, et c'est
                // précisément le piège d'une récupération vers un service interne — un service qui
                // négocie en NTLM obtiendrait sinon un jeton que personne n'a voulu lui donner.
                Credentials = null,
                PreAuthenticate = false,

                // La connexion est ouverte sur l'adresse que le garde a validée, et non sur une
                // seconde résolution du nom. Sans cela, un hôte autorisé dont la réponse DNS bascule
                // entre le contrôle et la connexion atteignait une adresse que le garde n'avait jamais
                // vue. Le nom reste celui de la requête : l'indication de nom et la validation du
                // certificat portent donc toujours sur lui.
                ConnectCallback = ConnectToValidatedAddress,
            })
            .AddStandardResilienceHandler(resilience =>
            {
                // Ici, et non sur « HttpClient.Timeout ». Le gestionnaire de résilience prend la gestion
                // du délai à son compte et met celui du client à l'infini : régler le client aurait
                // laissé croire que le budget s'appliquait alors qu'il était remplacé en silence. Il ne
                // coïncidait avec le défaut que par chance, ce qu'un essai a montré.
                resilience.TotalRequestTimeout.Timeout =
                    TimeSpan.FromSeconds(WsdlLimits.Default.FetchTimeoutSeconds);

                resilience.AttemptTimeout.Timeout =
                    TimeSpan.FromSeconds(WsdlLimits.Default.FetchTimeoutSeconds / 3.0);

                // Le disjoncteur exige une fenêtre au moins double du délai d'une tentative.
                resilience.CircuitBreaker.SamplingDuration =
                    TimeSpan.FromSeconds(WsdlLimits.Default.FetchTimeoutSeconds);
            });
    }

    /// <summary>
    /// Ouvre la connexion sur une adresse que le garde a validée, et sur aucune autre.
    /// </summary>
    /// <remarks>
    /// Une requête qui n'en porte aucune ne se connecte pas. Le refus est bruyant parce qu'il doit
    /// l'être : un second chemin vers le réseau contournerait tous les contrôles de ce dossier, et il
    /// vaut mieux l'apprendre à la première tentative qu'après coup.
    /// </remarks>
    private static async ValueTask<Stream> ConnectToValidatedAddress(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        if (PinnedAddress.Of(context.InitialRequestMessage) is not { Count: > 0 } pinned)
        {
            throw new HttpRequestException(
                "Cette requête ne porte aucune adresse validée : l'outil n'ouvre pas de connexion "
                + "qui n'est pas passée par le garde d'adresses.");
        }

        Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(
                    [.. pinned.Select(IPAddress.Parse)],
                    context.DnsEndPoint.Port,
                    cancellationToken)
                .ConfigureAwait(false);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();

            throw;
        }
    }
}
