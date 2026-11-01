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
    public static SchemaDraft Build(WsdlContract contract, string title, string version, string endpointPath)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointPath);
        if (endpointPath[0] != '/')
        {
            throw new ArgumentException("Le point d'accès doit être un chemin HTTP absolu.", nameof(endpointPath));
        }

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
            if (binding is null || operation.Pattern is MessagePattern.Notification or MessagePattern.SolicitResponse)
            {
                continue;
            }

            string? mediaType = SoapOpenApiOperation.RequestMediaType(binding.Soap);
            if (mediaType is null)
            {
                continue;
            }

            string path = endpointPath;
            if (!routes.Add(path))
            {
                throw new InvalidOperationException(
                    "Plusieurs opérations SOAP utilisent la même route OpenAPI : " + path);
            }

            PopulateOperation(paths.Section(path).Section(SoapOpenApiOperation.HttpMethod), operation, binding, mediaType);
        }

        return document;
    }

    private static void PopulateOperation(
        SchemaDraft post, WsdlOperation operation, WsdlBinding binding, string mediaType)
    {
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

        SchemaDraft success = post.Section("responses").Section(operation.HasResponse ? "200" : "202");
        success.Set("description", operation.HasResponse ? "Réponse SOAP" : "Message SOAP accepté");
        if (operation.HasResponse)
        {
            SetXmlContent(success.Section("content").Section(mediaType), binding.Soap);
        }

        if (binding.SoapAction is not null)
        {
            post.Set("x-soap-action", binding.SoapAction);
        }
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
