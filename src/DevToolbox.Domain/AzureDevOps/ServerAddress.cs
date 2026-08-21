namespace DevToolbox.Domain.AzureDevOps;

/// <summary>
/// Interprète ce qu'un développeur écrit lorsqu'on lui demande où se trouve son serveur Azure DevOps.
/// </summary>
/// <remarks>
/// <para>
/// Dans un parc interne, un serveur se désigne par son nom court : <c>azure</c>, parfois <c>azure/</c>. C'est
/// l'usage de la maison, et le refuser au motif qu'il manque un schéma reviendrait à demander au développeur
/// de saisir une forme qu'il n'écrit nulle part ailleurs. Le schéma sous-entendu est donc ajouté, jamais
/// deviné à la place d'un schéma explicite.
/// </para>
/// <para>
/// Un port n'est pas un schéma : <c>azure:8080</c> se lit comme un hôte et un port, ce qu'une lecture naïve
/// prendrait pour le schéma <c>azure</c>. La présence d'un schéma se juge donc sur le délimiteur complet,
/// et non sur les deux points.
/// </para>
/// </remarks>
public static class ServerAddress
{
    /// <summary>
    /// Normalise une adresse saisie, en complétant le schéma sous-entendu.
    /// </summary>
    /// <param name="value">Ce qui a été saisi ou enregistré.</param>
    /// <param name="allowInsecureHttp">Autorise une adresse en HTTP simple.</param>
    /// <param name="address">L'adresse retenue, sans requête ni fragment.</param>
    /// <param name="problem">Ce qui empêche la saisie d'être retenue.</param>
    /// <returns><see langword="true"/> si la saisie a donné une adresse utilisable.</returns>
    public static bool TryNormalise(
        string? value,
        bool allowInsecureHttp,
        out Uri? address,
        out ServerAddressProblem problem)
    {
        address = null;
        string candidate = value?.Trim() ?? string.Empty;

        if (candidate.Length == 0)
        {
            problem = ServerAddressProblem.Empty;
            return false;
        }

        if (!candidate.Contains(Uri.SchemeDelimiter, StringComparison.Ordinal))
        {
            candidate = Uri.UriSchemeHttps + Uri.SchemeDelimiter + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed))
        {
            problem = ServerAddressProblem.Malformed;
            return false;
        }

        problem = Diagnose(parsed, allowInsecureHttp);

        if (problem != ServerAddressProblem.None)
        {
            return false;
        }

        // La requête et le fragment n'ont aucun sens sur une adresse de base : ils sont écartés ici plutôt
        // que reportés sur chaque route construite ensuite.
        address = new Uri(parsed.GetLeftPart(UriPartial.Path), UriKind.Absolute);

        return true;
    }

    private static ServerAddressProblem Diagnose(Uri parsed, bool allowInsecureHttp)
    {
        if (string.IsNullOrEmpty(parsed.Host))
        {
            return ServerAddressProblem.MissingHost;
        }

        // Une adresse ne porte pas d'identité : la session Windows est la seule qui existe ici.
        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            return ServerAddressProblem.CarriesCredentials;
        }

        if (parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ServerAddressProblem.None;
        }

        if (parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return allowInsecureHttp ? ServerAddressProblem.None : ServerAddressProblem.InsecureScheme;
        }

        return ServerAddressProblem.UnsupportedScheme;
    }
}
