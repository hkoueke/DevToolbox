using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Extrait uniquement les chemins HTTP des adresses SOAP déclarées dans les
/// documents WSDL déjà résolus. Les autorités, identifiants et requêtes ne
/// quittent jamais le parseur.
/// </summary>
public static class SoapEndpointPathReader
{
    /// <summary>
    /// Lit les endpoints locaux des documents du contrat sans résoudre de réseau.
    /// Refuse les bindings ambigus et les chemins d'adresse non exploitables.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Read(WsdlContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        foreach (WsdlDocument document in contract.Documents.Where(
            item => item.Kind == WsdlDocumentKind.Definitions))
        {
            using XmlReader reader = XmlReader.Create(document.Path,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0,
                });
            XDocument xml = XDocument.Load(reader);
            XNamespace wsdl = "http://schemas.xmlsoap.org/wsdl/";
            foreach (XElement service in xml.Root?.Elements(wsdl + "service") ?? [])
            {
                foreach (XElement port in service.Elements(wsdl + "port"))
                {
                    string? bindingQName = port.Attribute("binding")?.Value;
                    if (string.IsNullOrWhiteSpace(bindingQName))
                    {
                        continue;
                    }

                    string localBinding = bindingQName[(bindingQName.LastIndexOf(':') + 1)..];
                    string? path = ReadPath(port, localBinding);
                    if (path is null)
                    {
                        continue;
                    }

                    if (paths.TryGetValue(localBinding, out string? previous)
                        && !string.Equals(previous, path, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Endpoints SOAP ambigus pour le binding " + localBinding);
                    }

                    paths[localBinding] = path;
                }
            }
        }

        return paths;
    }
    private static string? ReadPath(XElement port, string binding)
    {
        XElement[] addresses =
        [
            .. port.Elements().Where(element =>
                string.Equals(element.Name.LocalName, "address", StringComparison.Ordinal)
                && (string.Equals(element.Name.NamespaceName,
                    "http://schemas.xmlsoap.org/wsdl/soap/", StringComparison.Ordinal)
                    || string.Equals(element.Name.NamespaceName,
                        "http://schemas.xmlsoap.org/wsdl/soap12/", StringComparison.Ordinal))),
        ];
        if (addresses.Length == 0)
        {
            return null;
        }

        if (addresses.Length != 1
            || !Uri.TryCreate(addresses[0].Attribute("location")?.Value,
                UriKind.Absolute, out Uri? endpoint)
            || endpoint.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || endpoint.AbsolutePath.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Adresse SOAP non exploitable pour le binding " + binding);
        }

        return endpoint.AbsolutePath;
    }
}
