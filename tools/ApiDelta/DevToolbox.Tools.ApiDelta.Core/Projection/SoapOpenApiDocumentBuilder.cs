using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Construit un document OpenAPI 3.0 décrivant les points d'accès HTTP SOAP.</summary>
/// <remarks>
/// Les corps sont décrits comme XML opaque : le contrat SOAP est préservé sans
/// inventer de représentation JSON ni prétendre déduire une enveloppe exacte.
/// </remarks>
public static class SoapOpenApiDocumentBuilder
{
    /// <summary>Projette les opérations SOAP liées dans un brouillon OpenAPI.</summary>
    public static SchemaDraft Build(WsdlContract contract, string title, string version)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        SchemaDraft document = new();
        document.Set("openapi", "3.0.3");
        SchemaDraft info = document.Section("info");
        info.Set("title", title);
        info.Set("version", version);

        SchemaDraft paths = document.Section("paths");
        HashSet<string> routes = new(StringComparer.Ordinal);

        foreach (WsdlOperation operation in contract.Operations)
        {
            WsdlBinding? binding = operation.Binding;
            if (binding is null)
            {
                continue;
            }

            string? mediaType = SoapOpenApiOperation.RequestMediaType(binding.Soap);
            if (mediaType is null)
            {
                continue;
            }

            string path = "/" + Uri.EscapeDataString(operation.Name);
            if (!routes.Add(path))
            {
                throw new InvalidOperationException(
                    "Plusieurs opérations SOAP utilisent la même route OpenAPI : " + path);
            }

            SchemaDraft post = paths.Section(path).Section(SoapOpenApiOperation.HttpMethod);
            post.Set("operationId", operation.Name);
            post.Set("summary", operation.Name);
            if (!string.IsNullOrWhiteSpace(operation.Documentation))
            {
                post.Set("description", operation.Documentation);
            }

            if (operation.Input is not null)
            {
                SchemaDraft request = post.Section("requestBody");
                request.Set("required", true);
                SetXmlContent(request.Section("content").Section(mediaType), binding.Soap);
            }

            SchemaDraft responses = post.Section("responses");
            SchemaDraft success = responses.Section(operation.HasResponse ? "200" : "202");
            success.Set("description", operation.HasResponse ? "Réponse SOAP" : "Message SOAP accepté");
            if (operation.HasResponse)
            {
                SetXmlContent(success.Section("content").Section(mediaType), binding.Soap);
            }

            if (!string.IsNullOrWhiteSpace(binding.SoapAction))
            {
                post.Set("x-soap-action", binding.SoapAction);
            }
        }

        return document;
    }

    private static void SetXmlContent(SchemaDraft media, SoapVersion version)
    {
        SchemaDraft schema = media.Section("schema");
        schema.Set("type", "string");
        schema.Set("description",
            "Enveloppe SOAP XML (" + SoapOpenApiOperation.EnvelopeNamespace(version)
            + "). Structure détaillée à déduire des messages et schémas WSDL.");
    }
}
