using System.Text;
using System.Text.Json;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
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
        cancellationToken.ThrowIfCancellationRequested();
        // Confined readers are synchronous; keep cancellation boundaries around graph projection.
        SchemaDraft draft = new SoapDocumentExporter(SoapExportGraph.Read(wsdlPath)).Build(title, version);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        return Serialize(draft);
    }

    /// <summary>Exporte un contrat complet dont les documents locaux sont disponibles.</summary>
    public static string Export(WsdlContract contract, string title, string version)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!contract.IsComplete)
        {
            throw new InvalidOperationException("Le graphe WSDL doit être entièrement résolu.");
        }

        if (contract.Documents.Count == 0)
        {
            throw new InvalidOperationException("Aucun endpoint SOAP exploitable.");
        }

        return Serialize(new SoapDocumentExporter(SoapExportGraph.Read(contract.Entry.Path)).Build(title, version));
    }

    private static string Serialize(SchemaDraft draft)
    {
        object? document = Convert(draft);
        ValidateOpenApi(document);
        string yaml = Encoding.UTF8.GetString(OpenApiYamlExporter.Export(draft).Span);
        // Validate the actual serialized YAML shape through a JSON bridge: no second YAML parser.
        object? serialized = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(yaml);
        ValidateOpenApi(serialized);
        return yaml;
    }

    /// <summary>Rejette un export dont la structure ne respecte pas OpenAPI.</summary>
    internal static void ValidateOpenApi(object? document)
    {
        string json = JsonSerializer.Serialize(document);
        ReadResult parsed;
        try
        {
            parsed = OpenApiDocument.Parse(json, "json");
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or ArgumentException or NotSupportedException or UriFormatException)
        {
            throw new InvalidOperationException("Le document OpenAPI généré est invalide.", exception);
        }

        if (parsed.Document is null || parsed.Diagnostic?.Errors.Count > 0
            || parsed.Document.Validate(ValidationRuleSet.GetDefaultRuleSet()).Any())
        {
            throw new InvalidOperationException("Le document OpenAPI généré est invalide.");
        }
    }

    private static object? Convert(object? value) => value switch
    {
        SchemaDraft draft => draft.Keywords.ToDictionary(
            pair => pair.Key, pair => Convert(pair.Value), StringComparer.Ordinal),
        List<object?> values => values.Select(Convert).ToList(),
        _ => value,
    };
}
