using System.Text;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>
/// Attribue à chaque QName de type XSD une clé OpenAPI stable, sans collision.
/// Conserve les noms locaux lorsque ceux-ci sont uniques afin de préserver
/// les projections historiques.
/// </summary>
public sealed class SchemaNameRegistry
{
    private readonly IReadOnlyDictionary<string, string> _names;

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
            foreach (string name in names)
            {
                if (collisions.Contains(mapped[name]))
                {
                    mapped[name] = "qname__" + Convert.ToHexString(Encoding.UTF8.GetBytes(name));
                }
            }
        }

        _names = mapped;
    }

    public string Key(string qualifiedName) =>
        _names.TryGetValue(qualifiedName, out string? name)
            ? name
            : throw new KeyNotFoundException("Type XSD inconnu : " + qualifiedName);

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
