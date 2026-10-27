using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>
/// Écrit sur le disque les documents récupérés, et garde la trace de l'adresse d'où chacun vient.
/// </summary>
/// <remarks>
/// Matérialiser est ce qui permet à une entrée nouvelle d'obéir aux règles déjà en place au lieu d'exiger
/// des exceptions : un document matérialisé a un chemin, une taille et une empreinte, il se relit, et la
/// séance se reproduit depuis lui. Un document gardé en mémoire laisserait le développeur incapable de
/// dire ce qu'il a comparé.
/// <para>
/// Deux documents récupérés peuvent porter le même nom de fichier — <c>types.xsd</c> dans deux dossiers du
/// serveur est banal — et le second écraserait le premier en silence. Chacun reçoit donc un nom distinct
/// tiré de son origine, et la correspondance est conservée : le nom sur le disque sans l'adresse dont il
/// vient ne dirait plus ce qu'on a lu.
/// </para>
/// </remarks>
public sealed class Materialiser
{
    private readonly Dictionary<string, MaterialisedDocument> _written = new(StringComparer.Ordinal);
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Les documents écrits, par adresse d'origine.</summary>
    public IReadOnlyDictionary<string, MaterialisedDocument> Written => _written;

    /// <summary>
    /// Écrit un document récupéré, sous un nom qui n'écrase rien.
    /// </summary>
    /// <param name="origin">L'adresse d'où il vient.</param>
    /// <param name="content">Ses octets, tels qu'ils sont arrivés.</param>
    /// <param name="directory">Où l'écrire.</param>
    /// <param name="retrieved">L'instant de la récupération.</param>
    /// <returns>Le document écrit.</returns>
    /// <remarks>
    /// L'écriture passe par un fichier temporaire que l'on déplace ensuite : une récupération interrompue
    /// ne doit pas laisser un document à moitié écrit, qui se relirait comme un contrat tronqué au lieu
    /// de se signaler comme une séance inachevée.
    /// </remarks>
    public MaterialisedDocument Materialise(
        Uri origin, ReadOnlyMemory<byte> content, string directory, DateTimeOffset retrieved)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (_written.TryGetValue(origin.ToString(), out MaterialisedDocument? already))
        {
            return already;
        }

        _ = Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, Reserve(origin));
        string staging = path + ".partiel";

        File.WriteAllBytes(staging, content.Span);
        File.Move(staging, path, overwrite: false);

        MaterialisedDocument document = new(
            path,
            RetrievedOrigin.From(origin, retrieved, []),
            content.Length,
            Convert.ToHexStringLower(SHA256.HashData(content.Span)));

        _written[origin.ToString()] = document;

        return document;
    }

    /// <summary>Retient un nom de fichier libre pour une adresse.</summary>
    private string Reserve(Uri origin)
    {
        string proposed = Sanitise(Path.GetFileName(origin.AbsolutePath));

        if (_names.Add(proposed))
        {
            return proposed;
        }

        // Le nom est pris par un autre document. La distinction vient de l'origine, et non d'un compteur :
        // elle est stable d'une séance à l'autre, si bien que deux exécutions sur le même service
        // produisent les mêmes fichiers — ce que « types-2.xsd » ne garantirait pas.
        string stem = Path.GetFileNameWithoutExtension(proposed);
        string extension = Path.GetExtension(proposed);
        string distinct = stem + "-" + Mark(origin) + extension;

        _ = _names.Add(distinct);

        return distinct;
    }

    /// <summary>Une marque courte et stable tirée de l'adresse entière.</summary>
    private static string Mark(Uri origin) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(origin.ToString())))[..8];

    /// <summary>
    /// Rend un nom de fichier sûr à partir du dernier segment d'une adresse.
    /// </summary>
    /// <remarks>
    /// Le serveur choisit ce segment, pas nous. Un <c>..%2f..%2fetc%2fpasswd</c> décodé par
    /// <see cref="Uri"/> redeviendrait un chemin, et le combiner suffirait à écrire ailleurs : seul le
    /// nom de fichier est retenu, et les séparateurs qui subsisteraient sont remplacés.
    /// </remarks>
    private static string Sanitise(string proposed)
    {
        string bare = proposed.Trim();

        if (bare.Length == 0 || bare.All(character => character == '.'))
        {
            return "contrat.wsdl";
        }

        StringBuilder safe = new(bare.Length);

        foreach (char character in bare)
        {
            _ = safe.Append(
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character);
        }

        string clean = safe.ToString();

        return clean.Length <= 128
            ? clean
            : clean[..64] + "-" + clean.Length.ToString(CultureInfo.InvariantCulture);
    }
}
