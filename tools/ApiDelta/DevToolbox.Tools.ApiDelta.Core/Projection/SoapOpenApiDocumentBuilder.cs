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
        if (!ValidEndpointPath(endpointPath))
        {
            throw new ArgumentException("Le chemin SOAP doit être absolu, sans requête, fragment ni autorité.", nameof(endpointPath));
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

        document.Section("paths");
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
    /// Projette plusieurs endpoints SOAP à partir d'une correspondance explicite
    /// entre noms de bindings WSDL et chemins HTTP fournis par l'appelant.
    /// </summary>
    /// <remarks>
    /// Aucun chemin n'est dérivé du nom d'une opération. Un binding non référencé
    /// n'est pas exposé, et plusieurs bindings partageant un chemin sont regroupés
    /// dans une seule opération HTTP POST.
    /// </remarks>
    public static SchemaDraft Build(
        WsdlContract contract, string title, string version,
        IReadOnlyDictionary<string, string> endpointPaths)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(endpointPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        SchemaDraft document = new();
        document.Set("openapi", "3.0.3");
        SchemaDraft info = document.Section("info");
        info.Set("title", title);
        info.Set("version", version);
        SchemaDraft paths = document.Section("paths");

        Dictionary<string, List<WsdlOperation>> groups = new(StringComparer.Ordinal);
        foreach (WsdlOperation operation in contract.Operations)
        {
            if (operation.Binding is not { Soap: not SoapVersion.None } binding
                || operation.Pattern is not (MessagePattern.OneWay or MessagePattern.RequestResponse)
                || !endpointPaths.TryGetValue(binding.Name, out string? path))
            {
                continue;
            }

            if (!ValidEndpointPath(path))
            {
                throw new ArgumentException(
                    "Le chemin SOAP doit être absolu, sans requête ni fragment : " + binding.Name,
                    nameof(endpointPaths));
            }

            if (!groups.TryGetValue(path, out List<WsdlOperation>? group))
            {
                group = [];
                groups.Add(path, group);
            }

            group.Add(operation);
        }

        foreach (KeyValuePair<string, List<WsdlOperation>> group in groups.OrderBy(
            entry => entry.Key, StringComparer.Ordinal))
        {
            SchemaDraft post = paths.Section(group.Key).Section(SoapOpenApiOperation.HttpMethod);
            if (group.Value.Count == 1)
            {
                WsdlOperation operation = group.Value[0];
                PopulateOperation(post, operation, operation.Binding!,
                    SoapOpenApiOperation.RequestMediaType(operation.Binding!.Soap)!);
            }
            else
            {
                PopulateSharedEndpoint(post, group.Value);
            }
        }

        return document;
    }

    /// <summary>Accepte uniquement un chemin HTTP, jamais une URL avec autorité ou secrets.</summary>
    private static bool ValidEndpointPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path[0] == '/'
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Contains('?', StringComparison.Ordinal)
        && !path.Contains('#', StringComparison.Ordinal)
        && !path.Contains('\\\\', StringComparison.Ordinal)
        && !path.Any(char.IsControl);

    /// <summary>Représente les opérations par un POST standard sur leur endpoint commun.</summary>
    private static void PopulateSharedEndpoint(SchemaDraft post, List<WsdlOperation> operations)
    {
        post.Set("operationId", "soapEndpoint");
        post.Set("summary", "Point d'accès SOAP partagé");
        post.Set("description", DescribeOperations(operations));

        SchemaDraft request = post.Section("requestBody");
        request.Set("required", true);
        SchemaDraft content = request.Section("content");
        foreach (WsdlOperation operation in operations)
        {
            WsdlBinding binding = operation.Binding!;
            SetXmlContent(
                content.Section(SoapOpenApiOperation.RequestMediaType(binding.Soap)!),
                binding.Soap);
        }

        SchemaDraft responses = post.Section("responses");
        if (operations.Any(operation => operation.HasResponse))
        {
            responses.Section("200").Set("description", "Réponse SOAP pour les opérations avec réponse");
        }

        if (operations.Any(operation => !operation.HasResponse))
        {
            responses.Section("202").Set("description", "Message SOAP accepté sans réponse métier");
        }
        foreach (WsdlOperation operation in operations.Where(operation => operation.HasResponse))
        {
            WsdlBinding binding = operation.Binding!;
            SetXmlContent(
                responses.Section("200").Section("content")
                    .Section(SoapOpenApiOperation.RequestMediaType(binding.Soap)!),
                binding.Soap);
        }
    }

    private static string DescribeOperations(List<WsdlOperation> operations)
    {
        List<string> descriptions = ["Opérations SOAP exposées par ce point d'accès :"];
        foreach (WsdlOperation operation in operations)
        {
            WsdlBinding binding = operation.Binding!;
            string action = binding.SoapAction is null
                ? "SOAPAction non spécifiée"
                : "SOAPAction = " + (char)34 + binding.SoapAction + (char)34;
            descriptions.Add(
                "- " + operation.Name + " (" + operation.Pattern + ", " + action + ")");
        }

        return string.Join("\\n", descriptions);
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
            post.Set("description",
                (operation.Documentation ?? operation.Name) + "\nSOAPAction = " + (char)34 + binding.SoapAction + (char)34);
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
