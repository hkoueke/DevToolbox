namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>D'où un document récupéré est venu, et quand.</summary>
/// <remarks>
/// La redaction a lieu à la construction, et c'est tout l'intérêt du type : aucune instance ne peut
/// porter une adresse non caviardée. Le registre de masquage du puits de journalisation reste une seconde
/// ligne, pas la première — une adresse qu'un développeur a collée porte un jeton aussi souvent qu'un
/// <c>soap:address</c> porte un mot de passe, et il suffit d'un message de diagnostic écrit à la hâte
/// pour la publier.
/// </remarks>
public sealed record RetrievedOrigin
{
    private RetrievedOrigin(
        string redactedUrl, DateTimeOffset retrievedAt, IReadOnlyList<string> redirectChain)
    {
        RedactedUrl = redactedUrl;
        RetrievedAt = retrievedAt;
        RedirectChain = redirectChain;
    }

    /// <summary>L'adresse d'où le document vient, caviardée.</summary>
    public string RedactedUrl { get; }

    /// <summary>
    /// L'instant de la récupération.
    /// </summary>
    /// <remarks>
    /// Destiné au bloc de métadonnées d'exécution, et à lui seul : porté ailleurs, il rendrait deux
    /// exécutions sur les mêmes fichiers matérialisés différentes l'une de l'autre.
    /// </remarks>
    public DateTimeOffset RetrievedAt { get; }

    /// <summary>La chaîne de redirections effectivement suivie, caviardée à chaque saut.</summary>
    public IReadOnlyList<string> RedirectChain { get; }

    /// <summary>Note l'origine d'un document récupéré, en caviardant ce qui peut porter un secret.</summary>
    /// <param name="url">L'adresse d'où il vient.</param>
    /// <param name="retrievedAt">L'instant de la récupération.</param>
    /// <param name="redirectChain">Les sauts suivis, s'il y en a eu.</param>
    /// <returns>L'origine, sans aucun secret.</returns>
    public static RetrievedOrigin From(
        Uri url, DateTimeOffset retrievedAt, IEnumerable<Uri>? redirectChain = null)
    {
        ArgumentNullException.ThrowIfNull(url);

        return new RetrievedOrigin(
            Redact(url),
            retrievedAt,
            [.. (redirectChain ?? []).Select(Redact)]);
    }

    /// <summary>
    /// Retire d'une adresse ce qui peut porter un secret : les identifiants et la chaîne de requête.
    /// </summary>
    /// <remarks>
    /// La chaîne de requête part entière plutôt que par nom de paramètre. Retirer <c>token</c> et
    /// <c>key</c> en laissant le reste serait une liste noire, et une liste noire se contourne par
    /// inadvertance dès qu'un partenaire nomme son paramètre autrement.
    /// </remarks>
    private static string Redact(Uri url)
    {
        UriBuilder builder = new(url)
        {
            UserName = string.Empty,
            Password = string.Empty,
        };

        if (!string.IsNullOrEmpty(builder.Query))
        {
            builder.Query = "(caviardé)";
        }

        return builder.Uri.ToString();
    }
}
