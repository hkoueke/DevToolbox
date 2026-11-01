using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Assemble un POST par véritable endpoint et des enveloppes XML document/literal.</summary>
internal sealed class SoapDocumentExporter(SoapExportGraph graph)
{
    private static readonly XNamespace Wsdl = SoapExportGraph.Wsdl;
    private readonly SchemaDraft _document = new();
    private readonly HashSet<string> _operationIds = new(StringComparer.Ordinal);

    internal SchemaDraft Build(string title, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        _document.Set("openapi", "3.0.3");
        _document.Section("info").Set("title", title);
        _document.Section("info").Set("version", version);
        _document.Section("info").Set("description",
            "Projection SOAP document/literal. Les autorités des endpoints sont omises. "
            + "Les statuts 200/202 et default sont des conventions HTTP. "
            + "OpenAPI ne valide ni l'ordre des éléments XML ni l'équivalence lexicale XSD ; "
            + "le WSDL/XSD reste le contrat de validation XML.");
        _document.Section("paths");
        SoapXmlSchemas schemas = new(graph.Schemas, _document.Section("components").Section("schemas"));
        foreach (XElement port in graph.Ports.OrderBy(
            port => SoapExportGraph.QName(port, "binding").ToString(), StringComparer.Ordinal)
            .ThenBy(port => port.Attribute("name")?.Value, StringComparer.Ordinal))
        {
            XElement binding = SoapExportGraph.Resolve(graph.Bindings, SoapExportGraph.QName(port, "binding"));
            XNamespace soap = SoapNamespace(binding);
            string path = Endpoint(port, soap);
            XElement portType = SoapExportGraph.Resolve(graph.PortTypes, SoapExportGraph.QName(binding, "type"));
            AddBinding(binding, portType, soap, path, schemas);
        }

        if (_document.Section("paths").Keywords.Count == 0)
        {
            throw new WsdlExportException("Aucun endpoint SOAP exploitable (NO_SUPPORTED_OPERATION).");
        }

        return _document;
    }

    private void AddBinding(XElement binding, XElement portType, XNamespace soap,
        string path, SoapXmlSchemas schemas)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (XElement bound in binding.Elements(Wsdl + "operation")
            .OrderBy(operation => operation.Attribute("name")?.Value, StringComparer.Ordinal))
        {
            string name = bound.Attribute("name")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new WsdlExportException("WSDL_OPERATION_UNRESOLVED_OR_AMBIGUOUS");
            }
            XElement[] candidates = [.. portType.Elements(Wsdl + "operation")
                .Where(operation => string.Equals(operation.Attribute("name")?.Value, name, StringComparison.Ordinal))];
            if (candidates.Length != 1 || !names.Add(name))
            {
                throw new WsdlExportException("WSDL_OPERATION_UNRESOLVED_OR_AMBIGUOUS");
            }

            XElement operation = candidates[0];
            XElement[] flows = [.. operation.Elements().Where(item => item.Name == Wsdl + "input"
                || item.Name == Wsdl + "output")];
            if (flows.Length is < 1 or > 2 || flows[0].Name != Wsdl + "input"
                || (flows.Length == 2 && flows[1].Name != Wsdl + "output"))
            {
                throw new WsdlExportException("WSDL_NOTIFICATION_OR_SOLICIT_RESPONSE_UNSUPPORTED");
            }

            ValidateStyle(binding, bound, soap);
            AddOperation(operation, bound, soap, path, schemas);
        }
    }

    private void AddOperation(XElement operation, XElement bound, XNamespace soap,
        string path, SoapXmlSchemas schemas)
    {
        string name = operation.Attribute("name")!.Value;
        string identity = path + "\n" + soap.NamespaceName + "\n" + name;
        if (!_operationIds.Add(identity))
        {
            throw new WsdlExportException("SOAP_OPERATION_IDENTITY_COLLISION");
        }

        SchemaDraft post = _document.Section("paths").Section(path).Section("post");
        // One HTTP operation can describe multiple SOAP dispatch actions through standard anyOf schemas.
        if (post.Get("operationId") is null)
        {
            post.Set("operationId", "soapEndpoint" + _document.Section("paths").Keywords.Count
                .ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        string? action = bound.Element(soap + "operation")?.Attribute("soapAction")?.Value;
        SoapActionPolicy.Validate(action);
        string description = name + (action is null ? " (SOAPAction absente)" : " (SOAPAction = \"" + action + "\")");
        post.Set("description", (post.Get("description") as string ?? string.Empty) + description + "\n");
        SchemaDraft request = post.Section("requestBody");
        request.Set("required", true);
        AddContent(request.Section("content"), soap, Message(operation.Element(Wsdl + "input")!,
            bound.Element(Wsdl + "input"), soap, schemas));
        XElement? output = operation.Element(Wsdl + "output");
        SchemaDraft response = post.Section("responses").Section(output is null ? "202" : "200");
        response.Set("description", output is null ? "Accepté sans réponse métier" : "Réponse SOAP");
        if (output is not null)
        {
            AddContent(response.Section("content"), soap,
                Message(output, bound.Element(Wsdl + "output"), soap, schemas));
        }

        AddFaults(post, operation, bound, soap, schemas);
    }

    private SchemaDraft Message(XElement flow, XElement? boundFlow,
        XNamespace soap, SoapXmlSchemas schemas)
    {
        XElement message = SoapExportGraph.Resolve(graph.Messages, SoapExportGraph.QName(flow, "message"));
        XElement body = LiteralBody(boundFlow, soap);
        string[]? selection = body.Attribute("parts")?.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> found = new(StringComparer.Ordinal);
        SchemaDraft payload = SoapXmlSchemas.Object();
        foreach (XElement part in message.Elements(Wsdl + "part"))
        {
            string? partName = part.Attribute("name")?.Value;
            if (partName is null || !found.Add(partName))
            {
                throw new WsdlExportException("WSDL_MESSAGE_PART_AMBIGUOUS");
            }

            if (selection is not null && !selection.Contains(partName, StringComparer.Ordinal))
            {
                continue;
            }

            if (part.Attribute("type") is not null || part.Attribute("element") is null)
            {
                throw new WsdlExportException("DOCUMENT_LITERAL_ELEMENT_PART_REQUIRED");
            }

            XName element = SoapExportGraph.QName(part, "element");
            if (payload.Section("properties").Get(element.LocalName) is not null)
            {
                throw new WsdlExportException("XML_BODY_LOCAL_NAME_COLLISION");
            }

            payload.Section("properties").Set(element.LocalName,
                schemas.Element(new XmlQualifiedName(element.LocalName, element.NamespaceName)));
            payload.Append("required", element.LocalName);
        }

        if (selection is not null && selection.Any(part => !found.Contains(part)))
        {
            throw new WsdlExportException("SOAP_BODY_PART_UNRESOLVED");
        }

        return Envelope(payload, soap);
    }

    private static XElement LiteralBody(XElement? flow, XNamespace soap)
    {
        if (flow is null || flow.Elements().Any(element => element.Name != soap + "body"))
        {
            throw new WsdlExportException("SOAP_HEADERS_OR_NON_BODY_EXTENSIONS_UNSUPPORTED");
        }

        XElement[] bodies = [.. flow.Elements(soap + "body")];
        if (bodies.Length != 1 || bodies[0].Attribute("use")?.Value is not "literal"
            || bodies[0].Attribute("encodingStyle") is not null)
        {
            throw new WsdlExportException("SOAP_ENCODED_OR_MISSING_LITERAL_BODY_UNSUPPORTED");
        }

        return bodies[0];
    }

    private void AddFaults(SchemaDraft post, XElement operation, XElement bound,
        XNamespace soap, SoapXmlSchemas schemas)
    {
        foreach (XElement fault in operation.Elements(Wsdl + "fault")
            .OrderBy(fault => fault.Attribute("name")?.Value, StringComparer.Ordinal))
        {
            string? name = fault.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new WsdlExportException("SOAP_FAULT_BINDING_UNRESOLVED_OR_ENCODED");
            }
            XElement[] matches = [.. bound.Elements(Wsdl + "fault").Where(item =>
                string.Equals(item.Attribute("name")?.Value, name, StringComparison.Ordinal))];
            if (matches.Length != 1 || !string.Equals(matches[0].Element(soap + "fault")?.Attribute("use")?.Value, "literal", StringComparison.Ordinal))
            {
                throw new WsdlExportException("SOAP_FAULT_BINDING_UNRESOLVED_OR_ENCODED");
            }

            // The detail parts follow the abstract fault message, not the normal output message.
            XElement synthetic = new(Wsdl + "input", new XElement(soap + "body", new XAttribute("use", "literal")));
            SchemaDraft detailEnvelope = Message(fault, synthetic, soap, schemas);
            SchemaDraft detail = (SchemaDraft)detailEnvelope.Section("properties").Get("Body")!;
            SchemaDraft response = post.Section("responses").Section("default");
            response.Set("description", "SOAP Fault déclaré ; aucun statut HTTP garanti.");
            AddContent(response.Section("content"), soap, SoapFaultSchemas.Envelope(detail, soap));
        }
    }

    private static SchemaDraft Envelope(SchemaDraft payload, XNamespace soap)
    {
        string space = EnvelopeNamespace(soap);
        SchemaDraft envelope = SoapXmlSchemas.Object();
        SoapXmlSchemas.Xml(envelope, "Envelope", space);
        SchemaDraft body = payload;
        SoapXmlSchemas.Xml(body, "Body", space);
        envelope.Section("properties").Set("Body", body);
        envelope.Append("required", "Body");
        return envelope;
    }

    private static void AddContent(SchemaDraft content, XNamespace soap, SchemaDraft envelope)
    {
        SoapVersion version = string.Equals(soap.NamespaceName, WsdlNamespaces.Soap11, StringComparison.Ordinal)
            ? SoapVersion.Soap11 : SoapVersion.Soap12;
        string media = SoapHttpSemantics.MediaType(version)!;
        content.Section(media).Section("schema").Append("anyOf", envelope);
    }

    internal static string EnvelopeNamespace(XNamespace soap) => string.Equals(soap.NamespaceName, WsdlNamespaces.Soap11, StringComparison.Ordinal)
        ? "http://schemas.xmlsoap.org/soap/envelope/" : "http://www.w3.org/2003/05/soap-envelope";

    private static XNamespace SoapNamespace(XElement binding)
    {
        XElement[] choices = [.. binding.Elements().Where(element =>
            string.Equals(element.Name.LocalName, "binding", StringComparison.Ordinal) && element.Name.NamespaceName is WsdlNamespaces.Soap11 or WsdlNamespaces.Soap12)];
        if (choices.Length != 1 || !string.Equals(choices[0].Attribute("transport")?.Value, "http://schemas.xmlsoap.org/soap/http", StringComparison.Ordinal))
        {
            throw new WsdlExportException("SOAP_BINDING_OR_TRANSPORT_UNSUPPORTED");
        }

        return choices[0].Name.Namespace;
    }

    private static void ValidateStyle(XElement binding, XElement operation, XNamespace soap)
    {
        string style = operation.Element(soap + "operation")?.Attribute("style")?.Value
            ?? binding.Element(soap + "binding")?.Attribute("style")?.Value ?? "document";
        if (!string.Equals(style, "document", StringComparison.Ordinal))
        {
            throw new WsdlExportException("SOAP_RPC_UNSUPPORTED");
        }
    }

    private static string Endpoint(XElement port, XNamespace soap)
    {
        XElement[] addresses = [.. port.Elements().Where(element => string.Equals(element.Name.LocalName, "address", StringComparison.Ordinal))];
        if (addresses.Length != 1 || addresses[0].Name != soap + "address"
            || !Uri.TryCreate(addresses[0].Attribute("location")?.Value, UriKind.Absolute, out Uri? address)
            || address.Scheme is not ("http" or "https") || address.UserInfo.Length > 0
            || address.Query.Length > 0 || address.Fragment.Length > 0)
        {
            throw new WsdlExportException("SOAP_ENDPOINT_UNSAFE_OR_AMBIGUOUS");
        }

        string path = address.AbsolutePath;
        string decoded = Uri.UnescapeDataString(path);
        if (decoded.StartsWith("//", StringComparison.Ordinal) || decoded.Any(char.IsControl)
            || decoded.Contains('\\') || decoded.Contains('{') || decoded.Contains('}'))
        {
            throw new WsdlExportException("SOAP_ENDPOINT_PATH_UNSAFE");
        }

        return path;
    }
}
