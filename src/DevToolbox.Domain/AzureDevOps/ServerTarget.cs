namespace DevToolbox.Domain.AzureDevOps;

/// <summary>
/// Là où la boîte à outils est pointée : un serveur Azure DevOps hébergé sur site, sa collection et la
/// version d'API à utiliser.
/// </summary>
/// <remarks>
/// Dans le noyau partagé plutôt que dans un outil, pour la même raison que
/// <see cref="ProjectIdentifier"/> : le second outil lira le même serveur via le même client.
/// </remarks>
public sealed class ServerTarget
{
    /// <summary>Longueur maximale d'un nom de collection au-delà de laquelle il est jugé malformé.</summary>
    public const int MaxCollectionLength = 64;

    /// <summary>Crée une cible.</summary>
    /// <param name="baseUrl">L'URL de base absolue, sans le segment de collection.</param>
    /// <param name="collection">La collection, par exemple <c>DefaultCollection</c>.</param>
    /// <param name="apiVersion">La version de l'API REST à demander.</param>
    /// <exception cref="ArgumentException">La collection ne peut pas figurer dans une URL.</exception>
    public ServerTarget(Uri baseUrl, string collection, string apiVersion)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);

        if (!IsSafeCollection(collection))
        {
            throw new ArgumentException(
                "A collection must be 1 to 64 characters and must not contain a path separator, '..', or a "
                + "control character.",
                nameof(collection));
        }

        BaseUrl = baseUrl;
        Collection = collection;
        ApiVersion = apiVersion;
    }

    /// <summary>L'URL de base absolue du serveur.</summary>
    public Uri BaseUrl { get; }

    /// <summary>La collection lue.</summary>
    public string Collection { get; }

    /// <summary>La version d'API envoyée à chaque requête.</summary>
    public string ApiVersion { get; }

    /// <summary>L'hôte du serveur, pour l'annonce de cible et les messages d'échec.</summary>
    public string Host => BaseUrl.Host;

    /// <summary>
    /// La seule manière sanctionnée d'insérer la collection dans une URL : validée à la construction et
    /// échappée ici.
    /// </summary>
    /// <returns>Un segment de collection échappé, sûr pour une URL.</returns>
    public string ToCollectionSegment() => Uri.EscapeDataString(Collection);

    /// <summary>Indique si un nom de collection candidat peut être placé sans risque dans une URL.</summary>
    /// <param name="value">Le candidat.</param>
    /// <returns><see langword="true"/> si la valeur est sûre.</returns>
    public static bool IsSafeCollection(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxCollectionLength
        && !value.Contains('/', StringComparison.Ordinal)
        && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains("..", StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    /// <inheritdoc />
    public override string ToString() => $"{Host} ▸ {Collection}";
}
