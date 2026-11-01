namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Convertit un WSDL local et ses imports confinés en YAML OpenAPI.</summary>
public interface IWsdlOpenApiExporter
{
    /// <summary>Génère le YAML sans écriture ni récupération réseau.</summary>
    Task<string> ExportAsync(
        string wsdlPath, string title, string version, CancellationToken cancellationToken);
}
