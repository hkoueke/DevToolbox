using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using YamlDotNet.Serialization;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Exporte un graphe WSDL résolu en YAML OpenAPI 3.0.3, sans accès réseau.</summary>
public static class SoapOpenApiYamlExporter
{
    /// <summary>
    /// Lit un fichier WSDL et ses imports locaux, puis exporte le document OpenAPI YAML.
    /// Aucune écriture de fichier ni récupération distante implicite.
    /// </summary>
    public static async Task<string> ExportFileAsync(
        string wsdlPath, string title, string version, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wsdlPath);
        string absolutePath = Path.GetFullPath(wsdlPath);
        string root = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Racine WSDL introuvable.");
        WsdlReadResult read = await new WsdlReader().ReadAsync(
            absolutePath, WsdlLimits.Default, ImportPolicy.ConfinedTo(root),
            cancellationToken).ConfigureAwait(false);
        if (!read.IsFaithful)
        {
            throw new InvalidOperationException(
                "Lecture WSDL refusée : le graphe n'est pas exploitable.");
        }

        return Export(read.Contract!, title, version);
    }

    /// <summary>Projette les endpoints SOAP déclarés et sérialise le résultat.</summary>
    public static string Export(WsdlContract contract, string title, string version)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!contract.IsComplete)
        {
            throw new InvalidOperationException("Le graphe WSDL doit être entièrement résolu.");
        }

        IReadOnlyDictionary<string, string> endpoints = SoapEndpointPathReader.Read(contract);
        SchemaDraft draft = SoapOpenApiDocumentBuilder.Build(contract, title, version, endpoints);
        if (draft.Section("paths").Keywords.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucun endpoint SOAP exploitable n'a été trouvé dans le contrat WSDL.");
        }

        return new SerializerBuilder().Build().Serialize(Convert(draft));
    }

    private static object? Convert(object? value) => value switch
    {
        SchemaDraft draft => draft.Keywords.ToDictionary(
            pair => pair.Key, pair => Convert(pair.Value), StringComparer.Ordinal),
        List<object?> values => values.Select(Convert).ToList(),
        _ => value,
    };
}
