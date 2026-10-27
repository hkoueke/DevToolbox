namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>
/// Dit, pour chaque document matérialisé, l'adresse d'où il vient — et réciproquement.
/// </summary>
/// <remarks>
/// Cette correspondance existe pour que matérialiser ne change pas le sens des imports. Un
/// <c>schemaLocation</c> relatif écrit dans un document récupéré se rapporte à l'adresse de ce document,
/// pas au dossier à plat où l'outil l'a écrit : il est donc résolu contre l'adresse, puis traduit en
/// chemin local par cette table.
/// <para>
/// Les deux autres manières de s'en sortir ont été écartées. Réécrire les <c>schemaLocation</c> dans les
/// fichiers écrits rendrait l'empreinte mensongère — elle ne désignerait plus ce que le serveur a servi.
/// Reproduire l'arborescence des adresses sur le disque marcherait, mais ferait dépendre les chemins de
/// la forme des URL d'un serveur, y compris de ses encodages.
/// </para>
/// </remarks>
public sealed class RetrievalMap
{
    private readonly Dictionary<string, string> _toLocal;
    private readonly Dictionary<string, string> _toOrigin;

    /// <summary>Crée la correspondance.</summary>
    /// <param name="pairs">Chaque adresse et le fichier qui la matérialise.</param>
    public RetrievalMap(IEnumerable<KeyValuePair<Uri, string>> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        _toLocal = new Dictionary<string, string>(StringComparer.Ordinal);
        _toOrigin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<Uri, string> pair in pairs)
        {
            _toLocal[pair.Key.ToString()] = pair.Value;
            _toOrigin[System.IO.Path.GetFullPath(pair.Value)] = pair.Key.ToString();
        }
    }

    /// <summary>La correspondance vide : rien n'a été récupéré.</summary>
    public static RetrievalMap Empty { get; } = new([]);

    /// <summary>Indique qu'aucun document n'a été récupéré.</summary>
    public bool IsEmpty => _toLocal.Count == 0;

    /// <summary>Le fichier qui matérialise une adresse, s'il existe.</summary>
    /// <param name="address">L'adresse.</param>
    /// <returns>Le chemin local, ou <see langword="null"/>.</returns>
    public string? Local(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return _toLocal.TryGetValue(address.ToString(), out string? path) ? path : null;
    }

    /// <summary>L'adresse d'où vient un fichier, s'il a été récupéré.</summary>
    /// <param name="path">Le chemin local.</param>
    /// <returns>L'adresse, ou <see langword="null"/> pour un fichier donné par chemin.</returns>
    public Uri? Origin(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return _toOrigin.TryGetValue(System.IO.Path.GetFullPath(path), out string? address)
            ? new Uri(address)
            : null;
    }
}
