using System.Globalization;
using System.Net;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>
/// Récupère un contrat depuis une adresse, l'écrit sur le disque, puis le lit depuis les fichiers.
/// </summary>
/// <remarks>
/// Le seul appel sortant de cette tranche, et le premier de la boîte. Ce qu'il ne fait pas compte autant
/// que ce qu'il fait : il n'acquiert ni n'envoie d'identifiant, ne suit aucun <c>soap:address</c> que
/// personne n'a demandé, et n'appelle jamais le service que le contrat décrit. Récupérer un contrat est
/// une lecture — les méthodes d'écriture HTTP sont d'ailleurs bannies dans tout ce dépôt.
/// <para>
/// Les redirections sont suivies à la main, l'automatique étant désactivé à l'enregistrement du client.
/// C'est la seule façon de contrôler chaque étape : une redirection qui mènerait d'une adresse publique
/// vers un service interne passerait sans être vue si la bibliothèque la suivait pour nous.
/// </para>
/// <para>
/// Une fois tout écrit, la lecture repart des <em>fichiers</em>, avec la politique confinée au dossier de
/// sortie. Le graphe récupéré ne bénéficie ainsi d'aucune indulgence : il subit exactement les contrôles
/// qu'un graphe donné par chemin subit.
/// </para>
/// </remarks>
public sealed class ContractFetcher : IContractFetcher
{
    private readonly HttpClient _client;
    private readonly AddressGuard _guard;
    private readonly IWsdlReader _reader;
    private readonly IClock _clock;

    /// <summary>Crée le récupérateur.</summary>
    /// <param name="client">Le client typé, issu de la fabrique.</param>
    /// <param name="guard">Le garde qui décide de chaque adresse.</param>
    /// <param name="reader">Le lecteur qui relira les fichiers écrits.</param>
    /// <param name="clock">L'horloge, pour l'instant de récupération.</param>
    public ContractFetcher(
        HttpClient client, AddressGuard guard, IWsdlReader reader, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(clock);

        _client = client;
        _guard = guard;
        _reader = reader;
        _clock = clock;
    }

    /// <summary>Indique que le budget a expiré, et que ce n'est pas le développeur qui a annulé.</summary>
    /// <remarks>
    /// La distinction compte : une annulation demandée n'est pas un refus, et la présenter comme un
    /// budget dépassé reprocherait à une configuration ce que quelqu'un vient de décider.
    /// </remarks>
    private static bool Expired(CancellationTokenSource budget, CancellationToken asked) =>
        budget.IsCancellationRequested && !asked.IsCancellationRequested;

    /// <summary>Le refus d'un budget de récupération dépassé, la limite nommée.</summary>
    private static string Overrun(WsdlLimits limits) =>
        "La récupération du contrat a dépassé son budget de "
        + limits.FetchTimeoutSeconds.ToString(CultureInfo.InvariantCulture)
        + " seconde(s). La limite se règle, et elle se nomme : « wsdl.limits.fetchTimeoutSeconds ».";

    /// <inheritdoc />
    public async Task<Result<WsdlContract>> FetchAsync(
        Uri entry,
        string outputDirectory,
        WsdlLimits limits,
        ImportPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(policy);

        Materialiser materialiser = new();
        Dictionary<Uri, string> correspondence = [];

        // Le budget de récupération que l'équipe a écrit s'applique ici, à la récupération du graphe
        // entier. Le gestionnaire de résilience du client porte, lui, un délai figé à l'enregistrement :
        // il borne une requête, et il ne pouvait pas connaître une configuration lue bien après. Un
        // budget qui ne mord pas est pire qu'absent — il donne la preuve d'une protection qu'on n'a pas,
        // et un graphe de vingt imports consommait vingt fois le délai d'une seule requête.
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        budget.CancelAfter(TimeSpan.FromSeconds(limits.FetchTimeoutSeconds));

        Result<MaterialisedDocument> entered;

        try
        {
            entered = await WalkAsync(
                entry, materialiser, correspondence, outputDirectory, limits, policy, budget.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (Expired(budget, cancellationToken))
        {
            // Le dépassement remonte en exception depuis le client, et doit ressortir en refus nommé :
            // une pile d'appels dirait « opération annulée » sans dire par qui ni pourquoi, et le
            // développeur chercherait un défaut de réseau au lieu de régler une limite.
            return Result.Fail<WsdlContract>(FailureReason.ServiceUnavailable, Overrun(limits));
        }

        if (Expired(budget, cancellationToken))
        {
            return Result.Fail<WsdlContract>(FailureReason.ServiceUnavailable, Overrun(limits));
        }

        if (!entered.IsSuccess)
        {
            return Result.Fail<WsdlContract>(entered.Failure!);
        }

        return await ReadAsync(
            entered.Value, outputDirectory, limits, new RetrievalMap(correspondence),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Récupère le document, puis ceux qu'il désigne, et ainsi de suite.
    /// </summary>
    /// <remarks>
    /// En largeur plutôt qu'en récursion, pour que le budget de documents soit la seule borne et que la
    /// pile n'en soit jamais une : la tranche précédente a montré qu'une forme plausible peut l'épuiser,
    /// et qu'un débordement de pile ne dit rien de ce qu'il était en train de lire.
    /// </remarks>
    private async Task<Result<MaterialisedDocument>> WalkAsync(
        Uri entry,
        Materialiser materialiser,
        Dictionary<Uri, string> correspondence,
        string outputDirectory,
        WsdlLimits limits,
        ImportPolicy policy,
        CancellationToken cancellationToken)
    {
        Queue<Uri> pending = new([entry]);
        HashSet<string> seen = new(StringComparer.Ordinal);
        MaterialisedDocument? entered = null;

        while (pending.Count > 0)
        {
            Uri address = pending.Dequeue();

            if (!seen.Add(address.ToString()))
            {
                continue;
            }

            if (correspondence.Count >= limits.MaxDocumentCount)
            {
                return Result.Fail<MaterialisedDocument>(
                    FailureReason.InvalidInput,
                    "Le graphe dépasse " + limits.MaxDocumentCount + " documents. La limite est nommée "
                        + "plutôt que la récupération abandonnée sans raison.");
            }

            Result<RetrievedContent> content =
                await RetrieveAsync(address, limits, policy, cancellationToken).ConfigureAwait(false);

            if (!content.IsSuccess)
            {
                return Result.Fail<MaterialisedDocument>(content.Failure!);
            }

            RetrievedContent got = content.Value;

            MaterialisedDocument written = materialiser.Materialise(
                got.Origin, got.Bytes, outputDirectory, _clock.UtcNow);

            correspondence[got.Origin] = written.Path;
            entered ??= written;

            foreach (Uri designated in GraphWalker.Designated(got.Bytes, got.Origin))
            {
                pending.Enqueue(designated);
            }
        }

        return Result.Success(entered!);
    }

    /// <summary>Relit depuis les fichiers, politique confinée au dossier de sortie.</summary>
    private async Task<Result<WsdlContract>> ReadAsync(
        MaterialisedDocument entry,
        string outputDirectory,
        WsdlLimits limits,
        RetrievalMap retrieved,
        CancellationToken cancellationToken)
    {
        WsdlReadResult read = await _reader.ReadAsync(
            entry.Path,
            limits,
            ImportPolicy.ConfinedTo(outputDirectory),
            cancellationToken,
            retrieved).ConfigureAwait(false);

        return read.IsFaithful
            ? Result.Success(read.Contract!)
            : Result.Fail<WsdlContract>(
                FailureReason.InvalidInput,
                "Le contrat récupéré n'a pas pu être lu : "
                    + string.Join(" · ", read.Diagnostics.Select(one => one.Message)));
    }

    /// <summary>
    /// Fait la requête, en revalidant chaque redirection.
    /// </summary>
    /// <remarks>
    /// Chaque saut repasse par le garde, et non seulement le premier : un serveur qui redirige est en
    /// train de désigner une adresse que personne n'a contrôlée, et c'est le moyen le plus simple de faire
    /// atteindre un service interne à un outil qui croit viser l'extérieur.
    /// </remarks>
    private async Task<Result<RetrievedContent>> RetrieveAsync(
        Uri address,
        WsdlLimits limits,
        ImportPolicy policy,
        CancellationToken cancellationToken)
    {
        Uri current = address;
        List<string> chain = [];

        for (int hop = 0; hop <= policy.MaxRedirects; hop++)
        {
            AddressCheck check = await _guard.CheckAsync(current, policy, cancellationToken)
                .ConfigureAwait(false);

            if (!check.IsAllowed)
            {
                return Result.Fail<RetrievedContent>(
                    FailureReason.PermissionDenied, Said(check.Refusal!.Message, chain));
            }

            // La requête emporte les adresses que le garde vient de valider, et la connexion s'ouvrira
            // sur l'une d'elles. Sans cet épinglage, le client résolvait le nom une seconde fois : un
            // hôte autorisé dont la réponse DNS bascule entre les deux atteignait une adresse que le
            // garde n'avait jamais vue, et le contrôle d'adresse interne tombait sans être touché.
            using HttpRequestMessage request = new(HttpMethod.Get, current);

            PinnedAddress.Pin(request, check.Resolved);

            using HttpResponseMessage response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (Redirected(response) is { } next)
            {
                chain.Add(RetrievedOrigin.From(next, _clock.UtcNow, []).RedactedUrl);
                current = next;

                continue;
            }

            return await ResponseReader
                .ReceiveAsync(response, current, chain, limits, cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Fail<RetrievedContent>(
            FailureReason.RedirectRefused,
            "La chaîne de redirections dépasse " + policy.MaxRedirects
                + " sauts. Suivie jusque-là : " + string.Join(" → ", chain));
    }

    /// <summary>L'adresse vers laquelle une réponse redirige, s'il y en a une.</summary>
    /// <remarks>
    /// Résolue contre l'adresse demandée, parce qu'un en-tête « Location » a le droit d'être relatif, et
    /// qu'un relatif pris pour un absolu désignerait n'importe quoi.
    /// </remarks>
    private static Uri? Redirected(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Found
            or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect))
        {
            return null;
        }

        if (response.Headers.Location is not { } location)
        {
            return null;
        }

        return new Uri(response.RequestMessage?.RequestUri ?? location, location);
    }

    private static string Said(string message, List<string> chain) =>
        chain.Count == 0 ? message : message + " Chaîne suivie : " + string.Join(" → ", chain);
}
