using System.Runtime.CompilerServices;
using System.Net;
using System.Text;
using System.Xml;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Configuration;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>
/// Reçoit une réponse : bornée en taille, refusée si elle demande un identifiant, refusée si ce n'est
/// pas du XML.
/// </summary>
/// <remarks>
/// Le refus d'une réponse qui n'est pas un document XML est l'échec <em>ordinaire</em> de cette tranche,
/// et non un cas exotique : une page de connexion de portail rendue avec un statut de succès est ce qui
/// arrive quand l'adresse est bonne mais la session absente. Elle doit être refusée en nommant ce qui est
/// arrivé — son type de contenu, sa taille, ses premiers caractères — et non en échouant à l'analyser,
/// car « erreur de syntaxe à la ligne 1 » envoie corriger un contrat qui n'a jamais été reçu.
/// </remarks>
internal static class ResponseReader
{
    /// <summary>Lit une réponse, ou la refuse en disant pourquoi.</summary>
    /// <param name="response">La réponse.</param>
    /// <param name="origin">L'adresse atteinte.</param>
    /// <param name="chain">Les redirections suivies.</param>
    /// <param name="limits">Le budget.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Le contenu, ou un échec déjà affichable.</returns>
    internal static async Task<Result<RetrievedContent>> ReceiveAsync(
        HttpResponseMessage response,
        Uri origin,
        IReadOnlyList<string> chain,
        WsdlLimits limits,
        CancellationToken cancellationToken)
    {
        if (Refused(response) is { } refusal)
        {
            return refusal;
        }

        Result<byte[]> bytes = await BoundedAsync(response, limits, cancellationToken)
            .ConfigureAwait(false);

        if (!bytes.IsSuccess)
        {
            return Result.Fail<RetrievedContent>(bytes.Failure!);
        }

        byte[] received = bytes.Value;

        return WellFormed(received, response, origin)
            ?? Result.Success(new RetrievedContent(origin, received, chain));
    }

    /// <summary>
    /// Refuse une réponse qui demande un identifiant, ou qui a échoué.
    /// </summary>
    /// <remarks>
    /// L'outil ne détient aucun identifiant et n'en acquerra pas : une fonctionnalité qui n'en détient
    /// aucun ne peut pas en divulguer. Le refus dit donc quoi faire à la place — télécharger le document
    /// et le passer comme fichier — plutôt que de laisser croire qu'un réglage manque.
    /// </remarks>
    private static Result<RetrievedContent>? Refused(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            or HttpStatusCode.ProxyAuthenticationRequired)
        {
            return Result.Fail<RetrievedContent>(
                FailureReason.AuthenticationFailed,
                "Cette adresse demande une authentification. L'outil n'acquiert ni n'envoie aucun "
                    + "identifiant : téléchargez le document vous-même et passez-le comme fichier.");
        }

        return response.IsSuccessStatusCode
            ? null
            : Result.Fail<RetrievedContent>(
                FailureReason.ServiceUnavailable,
                "Le serveur a répondu " + (int)response.StatusCode + " ("
                    + response.StatusCode + "). Le statut est dit tel quel, l'outil n'ayant pas à "
                    + "l'interpréter à la place du développeur.");
    }

    /// <summary>Lit le corps sans dépasser le budget, et sans le charger pour s'en apercevoir.</summary>
    private static async Task<Result<byte[]>> BoundedAsync(
        HttpResponseMessage response, WsdlLimits limits, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > limits.MaxResponseBytes)
        {
            return TooLarge(limits, response.Content.Headers.ContentLength.Value);
        }

        Stream body = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using ConfiguredAsyncDisposable _ = body.ConfigureAwait(false);

        using MemoryStream collected = new();

        byte[] buffer = new byte[81920];
        int read;

        while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (collected.Length + read > limits.MaxResponseBytes)
            {
                // Un en-tête de taille absent ou menteur ne doit pas suffire à dépasser le budget : il
                // est donc vérifié aussi pendant la lecture, et non seulement avant.
                return TooLarge(limits, collected.Length + read);
            }

            await collected.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success(collected.ToArray());
    }

    private static Result<byte[]> TooLarge(WsdlLimits limits, long observed) =>
        Result.Fail<byte[]>(
            FailureReason.InvalidInput,
            "La réponse pèse au moins " + observed + " octets, au-delà du budget de "
                + limits.MaxResponseBytes + ". La limite est nommée plutôt que la récupération "
                + "abandonnée sans raison.");

    /// <summary>Refuse une réponse qui n'est pas un document XML, en nommant ce qui est arrivé.</summary>
    private static Result<RetrievedContent>? WellFormed(
        byte[] bytes, HttpResponseMessage response, Uri origin)
    {
        try
        {
            using XmlReader reader = XmlReader.Create(
                new MemoryStream(bytes),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0,
                });

            while (reader.Read())
            {
                // Parcouru entièrement : un document bien formé jusqu'à la racine mais tronqué ensuite
                // doit être refusé ici, où l'on peut encore dire que la récupération est en cause.
            }

            return null;
        }
        catch (XmlException)
        {
            return Result.Fail<RetrievedContent>(
                FailureReason.InvalidInput, Describe(bytes, response, origin));
        }
    }

    /// <summary>Dit ce qui est arrivé, puisque ce n'était pas le contrat attendu.</summary>
    private static string Describe(byte[] bytes, HttpResponseMessage response, Uri origin) =>
        "« " + origin.Host + " » a répondu "
            + (response.Content.Headers.ContentType?.MediaType ?? "sans type de contenu")
            + ", " + bytes.Length + " octets, qui ne forment pas un document XML. Les premiers "
            + "caractères sont : « " + Head(bytes) + " ». Une page de connexion rendue avec un statut de "
            + "succès est le cas le plus courant.";

    /// <summary>Les premiers caractères de la réponse, sur une seule ligne.</summary>
    private static string Head(byte[] bytes)
    {
        string text = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 120));

        return text.ReplaceLineEndings(" ").Trim();
    }
}
