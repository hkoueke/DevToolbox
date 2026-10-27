using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>
/// Trouve les adresses qu'un document récupéré désigne, pour que le graphe entier soit matérialisé.
/// </summary>
/// <remarks>
/// Une lecture volontairement sommaire : seuls les emplacements sont cherchés, et rien n'est interprété.
/// Le document n'a pas encore été contrôlé — c'est la lecture qui s'en chargera, depuis les fichiers — et
/// en tirer un sens à ce stade donnerait du poids à un document dont on ne sait rien.
/// <para>
/// Les définitions de type de document sont interdites ici aussi. Ce n'est pas une redondance : ce
/// passage précède la lecture, et un contrôle qui ne vaudrait qu'au second passage laisserait le premier
/// ouvert.
/// </para>
/// </remarks>
internal static class GraphWalker
{
    private static readonly XNamespace Schema = XNamespace.Get(WsdlNamespaces.Schema);
    private static readonly XNamespace Wsdl = XNamespace.Get(WsdlNamespaces.Wsdl11);

    /// <summary>Les adresses que ce document désigne, résolues contre la sienne.</summary>
    /// <param name="content">Ses octets.</param>
    /// <param name="origin">L'adresse d'où il vient.</param>
    /// <returns>Les adresses à récupérer à leur tour.</returns>
    internal static IReadOnlyList<Uri> Designated(ReadOnlyMemory<byte> content, Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        XElement? root = Parse(content);

        if (root is null)
        {
            return [];
        }

        List<Uri> designated = [];

        foreach (string where in Locations(root))
        {
            if (Uri.TryCreate(origin, where, out Uri? address) && !address.IsFile)
            {
                designated.Add(address);
            }
        }

        return designated;
    }

    /// <summary>Les emplacements écrits dans le document, à n'importe quelle profondeur.</summary>
    /// <remarks>
    /// À n'importe quelle profondeur, parce qu'un <c>xs:schema</c> vit dans <c>wsdl:types</c> et qu'un
    /// <c>xs:import</c> peut vivre dans un <c>xs:redefine</c>. Chercher au premier niveau manquerait
    /// exactement les documents dont la lecture aurait ensuite besoin.
    /// </remarks>
    private static IEnumerable<string> Locations(XElement root) =>
        root.Descendants()
            .Where(Imports)
            .Select(element =>
                element.Attribute("schemaLocation")?.Value ?? element.Attribute("location")?.Value)
            .OfType<string>()
            .Where(where => !string.IsNullOrWhiteSpace(where))
            .Distinct(StringComparer.Ordinal);

    private static bool Imports(XElement element) =>
        (element.Name.Namespace == Schema
            && element.Name.LocalName is "import" or "include" or "redefine")
        || (element.Name.Namespace == Wsdl
            && string.Equals(element.Name.LocalName, "import", StringComparison.Ordinal));

    private static XElement? Parse(ReadOnlyMemory<byte> content)
    {
        try
        {
            using XmlReader reader = XmlReader.Create(
                new MemoryStream(content.ToArray()),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0,
                });

            return XDocument.Load(reader).Root;
        }
        catch (XmlException)
        {
            // Illisible : rien à suivre. Le refus appartient à la lecture, qui le dira avec sa position
            // et son code — le dire deux fois, et différemment, serait pire que le dire une fois.
            return null;
        }
    }
}
