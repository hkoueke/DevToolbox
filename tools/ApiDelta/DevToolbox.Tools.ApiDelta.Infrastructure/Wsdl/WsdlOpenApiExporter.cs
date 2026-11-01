using DevToolbox.Tools.ApiDelta.Core.Abstractions;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Adaptateur d'export SOAP pour la présentation.</summary>
public sealed class WsdlOpenApiExporter : IWsdlOpenApiExporter
{
    /// <inheritdoc />
    public Task<string> ExportAsync(
        string wsdlPath, string title, string version, CancellationToken cancellationToken) =>
        SoapOpenApiYamlExporter.ExportFileAsync(wsdlPath, title, version, cancellationToken);
}
