using System.Text;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>
/// Attribue à chaque QName de type XSD une clé OpenAPI stable, sans collision.
/// Conserve les noms locaux lorsque ceux-ci sont uniques afin de préserver
/// les projections historiques.
/// </summary>
public sealed class SchemaNameRegistry
{
    private readonly Dictionary<string, string> _names;

    /// <summary>Construit une correspondance déterministe et sans collisions pour les types fournis.</summary>
    /// <param name="qualifiedNames">Les noms qualifiés XSD à enregistrer.</param>
    public SchemaNameRegistry(IEnumerable<string> qualifiedNames)
    {
        ArgumentNullException.ThrowIfNull(qualifiedNames);

        string[] names = qualifiedNames.Distinct(StringComparer.Ordinal).ToArray();
        HashSet<string> ambiguous = names
            .GroupBy(Local, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, string> mapped = new(StringComparer.Ordinal);
        foreach (string name in names.OrderBy(name => name, StringComparer.Ordinal))
        {
            string local = Local(name);
            // Le suffixe hexadécimal encode le QName entier : aucune collision de hash.
            // Le séparateur double underscore ne peut pas rendre une clé ambiguë,
            // car toute clé candidate est ensuite vérifiée contre les autres.
            mapped.Add(name, ambiguous.Contains(local)
                ? local + "__" + Convert.ToHexString(Encoding.UTF8.GetBytes(name))
                : local);
        }

        // Un nom local peut ressembler à une clé qualifiée produite pour un autre type.
        // Dans ce cas, encoder aussi le nom local en conflit, de manière déterministe.
        while (mapped.Values.Distinct(StringComparer.Ordinal).Count() != mapped.Count)
        {
            HashSet<string> collisions = mapped.GroupBy(pair => pair.Value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string name in names.Where(name => collisions.Contains(mapped[name])))
            {
                mapped[name] = "qname__" + Convert.ToHexString(Encoding.UTF8.GetBytes(name));
            }
        }

        _names = mapped;
    }

    /// <summary>Retourne la clé du composant OpenAPI associé à un type enregistré.</summary>
    /// <param name="qualifiedName">Nom qualifié XSD.</param>
    /// <returns>La clé du composant.</returns>
    /// <exception cref="KeyNotFoundException">Le type n’est pas enregistré.</exception>
    public string Key(string qualifiedName) =>
        _names.TryGetValue(qualifiedName, out string? name)
            ? name
            : throw new KeyNotFoundException("Type XSD inconnu : " + qualifiedName);

    /// <summary>Construit un pointeur JSON vers le schéma, avec échappement RFC 6901.</summary>
    /// <param name="qualifiedName">Nom qualifié XSD.</param>
    /// <returns>Un pointeur local OpenAPI.</returns>
    public string Reference(string qualifiedName) =>
        "#/components/schemas/" + (_names.TryGetValue(qualifiedName, out string? name)
            ? name : Local(qualifiedName)).Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string Local(string qualifiedName)
    {
        int separator = qualifiedName.LastIndexOf(':');
        return separator < 0 ? qualifiedName : qualifiedName[(separator + 1)..];
    }
}
