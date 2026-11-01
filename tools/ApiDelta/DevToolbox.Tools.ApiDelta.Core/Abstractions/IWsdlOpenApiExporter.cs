using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Convertit un WSDL local, une collecte sauvegardée ou une URL en YAML OpenAPI.</summary>
public interface IWsdlOpenApiExporter
{
    /// <summary>Génère le YAML sans écriture ni récupération réseau.</summary>
    Task<string> ExportAsync(
        string wsdlPath, string title, string version, CancellationToken cancellationToken);

    /// <summary>Récupère un graphe autorisé, conserve une collecte rejouable et génère le YAML validé.</summary>
    Task<string> ExportUrlAsync(Uri source, WsdlUrlExportOptions options,
        string title, string version, CancellationToken cancellationToken);
}
