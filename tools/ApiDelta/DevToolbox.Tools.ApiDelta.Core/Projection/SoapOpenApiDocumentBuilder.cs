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

        List<WsdlOperation> operations =
        [
            .. contract.Operations.Where(operation =>
                operation.Binding is { Soap: not SoapVersion.None }
                && operation.Pattern is MessagePattern.OneWay or MessagePattern.RequestResponse),
        ];

        if (operations.Count > 0)
        {
            SchemaDraft post = document.Section("paths")
                .Section(endpointPath).Section(SoapOpenApiOperation.HttpMethod);

            if (operations.Count == 1)
            {
                WsdlOperation operation = operations[0];
                PopulateOperation(
                    post, operation, operation.Binding!,
                    SoapOpenApiOperation.RequestMediaType(operation.Binding!.Soap)!);
            }
            else
            {
                PopulateSharedEndpoint(post, operations);
            }
        }

        return document;
    }

    /// <summary>
    /// Un endpoint SOAP peut exposer plusieurs actions. OpenAPI n'autorise qu'un POST
    /// par chemin : les actions sont conservées dans une extension documentée, sans
    /// inventer d'URL ou de schéma discriminant qui n'existe pas dans le WSDL.
    /// </summary>
    private static void PopulateSharedEndpoint(SchemaDraft post, List<WsdlOperation> operations)
    {
        post.Set("operationId", "soapEndpoint");
        post.Set("summary", "Point d'accès SOAP partagé");
        post.Set("description",
            "Plusieurs opérations SOAP partagent ce point d'accès. " +
            "L'extension x-soap-operations conserve leur identité et leurs actions.");

        HashSet<string> mediaTypes = new(StringComparer.Ordinal);
        SchemaDraft content = post.Section("requestBody").Section("content");
        post.Section("requestBody").Set("required", true);

        foreach (WsdlOperation operation in operations)
        {
            WsdlBinding binding = operation.Binding!;
            string mediaType = SoapOpenApiOperation.RequestMediaType(binding.Soap)!;
            if (mediaTypes.Add(mediaType))
            {
                SetXmlContent(content.Section(mediaType), binding.Soap);
            }

            SchemaDraft entry = new();
            entry.Set("name", operation.Name);
            entry.Set("pattern", operation.Pattern.ToString());
            if (binding.SoapAction is not null)
            {
                entry.Set("soapAction", binding.SoapAction);
            }

            entry.Set("mediaType", mediaType);
            post.Append("x-soap-operations", entry);
        }

        SchemaDraft success = post.Section("responses").Section("200");
        success.Set("description", "Réponse SOAP, si l'opération en produit une");
        SchemaDraft responseContent = success.Section("content");
        foreach (WsdlOperation operation in operations.Where(operation => operation.HasResponse))
        {
            WsdlBinding binding = operation.Binding!;
            SetXmlContent(
                responseContent.Section(SoapOpenApiOperation.RequestMediaType(binding.Soap)!),
                binding.Soap);
        }
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
