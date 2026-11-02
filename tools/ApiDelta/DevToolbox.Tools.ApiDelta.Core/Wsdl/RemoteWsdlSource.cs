namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Contrôle les parties sensibles d'une URL avant sa sélection et toute persistance.</summary>
public static class RemoteWsdlSource
{
    /// <summary>Accepte uniquement les URL HTTP(S) sans authentification et à requête de métadonnées.</summary>
    /// <param name="address">L'adresse proposée.</param>
    /// <returns>Vrai si l'adresse ne porte aucune partie sensible interdite.</returns>
    public static bool IsSafe(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IsAbsoluteUri && address.Scheme is "http" or "https"
            && address.UserInfo.Length == 0 && address.Fragment.Length == 0 && IsMetadataQuery(address.Query);
    }

    /// <summary>Accepte les seules requêtes de métadonnées WSDL/XSD.</summary>
    /// <param name="query">La requête avec son point d'interrogation.</param>
    /// <returns>Vrai pour une requête absente ou de métadonnées.</returns>
    public static bool IsMetadataQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length == 0 || string.Equals(query, "?wsdl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(query, "?singleWsdl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] pair = query[1..].Split('=');
        return pair.Length == 2 && (pair[0] is "wsdl" or "xsd")
            && pair[1].StartsWith(pair[0], StringComparison.Ordinal)
            && pair[1].Length > pair[0].Length && pair[1][pair[0].Length..].All(char.IsAsciiDigit);
    }
}
