using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Graphe d'export hors ligne ; chaque référence est un nom XML étendu.</summary>
internal sealed class SoapExportGraph
{
    internal static readonly XNamespace Wsdl = WsdlNamespaces.Wsdl11;
    internal readonly Dictionary<XName, XElement> Messages = [];
    internal readonly Dictionary<XName, XElement> PortTypes = [];
    internal readonly Dictionary<XName, XElement> Bindings = [];
    internal readonly List<XElement> Ports = [];
    internal XmlSchemaSet Schemas { get; private set; } = new();

    internal static SoapExportGraph Read(string path)
    {
        string absolute = Path.GetFullPath(path);
        string rootDirectory = Path.GetDirectoryName(absolute)!;
        XElement root;
        try
        {
            root = Load(absolute);
        }
        catch (XmlException)
        {
            throw new WsdlExportException("XML_DTD_OR_PARSE_INVALID");
        }
        List<Diagnostic> diagnostics = [];
        WsdlImportWalker walker = new(WsdlLimits.Default,
            ImportPolicy.ConfinedTo(rootDirectory), RetrievalMap.Empty, diagnostics);
        walker.Walk(root, absolute);
        List<(string Path, XElement Root)> documents = [(absolute, root)];
        documents.AddRange(walker.Documents.Select(item => (item.Path, item.Root)));
        return Assemble(documents, rootDirectory, diagnostics);
    }

    private static SoapExportGraph Assemble(
        List<(string Path, XElement Root)> documents, string directory, List<Diagnostic> diagnostics)
    {
        SoapExportGraph graph = new();
        List<InlineSchema> schemas = [];
        foreach (var document in documents)
        {
            if (document.Root.Name != Wsdl + "definitions"
                || document.Root.Elements(Wsdl + "import").Any(import =>
                    string.IsNullOrWhiteSpace(import.Attribute("namespace")?.Value)
                    || string.IsNullOrWhiteSpace(import.Attribute("location")?.Value)))
            {
                throw new WsdlExportException("WSDL_IMPORT_OR_ROOT_INVALID");
            }

            Definitions? definitions = new DefinitionsReader(document.Path, diagnostics)
                .Read(document.Root, importsResolved: true);
            schemas.AddRange(definitions!.InlineSchemas);
            graph.Register(document.Root);
        }

        Definitions combined = new(null, [], [],
            new Dictionary<string, WsdlBinding>(StringComparer.Ordinal), [], schemas);
        SchemaSetResult compiled = SchemaCompiler.Compile(documents[0].Path, combined,
            WsdlLimits.Default, ImportPolicy.ConfinedTo(directory), RetrievalMap.Empty, diagnostics);
        if (diagnostics.Any(item => item.IsBlocking) || compiled.Unresolved.Count > 0
            || documents.Count + compiled.Documents.Count > WsdlLimits.Default.MaxDocumentCount)
        {
            throw new WsdlExportException("WSDL_XSD_GRAPH_UNRESOLVED_OR_INVALID");
        }

        graph.Schemas = compiled.Compiled;
        return graph;
    }

    private void Register(XElement root)
    {
        XNamespace space = root.Attribute("targetNamespace")?.Value ?? string.Empty;
        Register(root, space, "message", Messages);
        Register(root, space, "portType", PortTypes);
        Register(root, space, "binding", Bindings);
        Ports.AddRange(root.Elements(Wsdl + "service").Elements(Wsdl + "port"));
    }

    private static void Register(XElement root, XNamespace space, string kind,
        Dictionary<XName, XElement> index)
    {
        foreach (XElement element in root.Elements(Wsdl + kind))
        {
            string? name = element.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name) || !index.TryAdd(space + name, element))
            {
                throw new WsdlExportException("WSDL_DECLARATION_MISSING_OR_AMBIGUOUS");
            }
        }
    }

    internal static XElement Load(string path)
    {
        if (new FileInfo(path).Length > WsdlLimits.Default.MaxDocumentBytes)
        {
            throw new WsdlExportException("DOCUMENT_BYTE_LIMIT");
        }

        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = WsdlLimits.Default.MaxDocumentBytes,
        });
        XElement root = XElement.Load(reader, LoadOptions.SetLineInfo);
        Queue<(XElement Element, int Depth)> pending = new();
        pending.Enqueue((root, 1));
        while (pending.TryDequeue(out var item))
        {
            if (item.Depth > WsdlLimits.Default.MaxNestingDepth)
            {
                throw new WsdlExportException("DOCUMENT_DEPTH_LIMIT");
            }

            foreach (XElement child in item.Element.Elements())
            {
                pending.Enqueue((child, item.Depth + 1));
            }
        }

        return root;
    }

    internal static XName QName(XElement carrier, string attribute)
    {
        string value = carrier.Attribute(attribute)?.Value.Trim()
            ?? throw new WsdlExportException("QNAME_MISSING");
        try
        {
            int colon = value.IndexOf(':', StringComparison.Ordinal);
            string local = colon < 0 ? value : value[(colon + 1)..];
            XmlConvert.VerifyNCName(local);
            XNamespace space = colon < 0 ? carrier.GetDefaultNamespace()
                : carrier.GetNamespaceOfPrefix(XmlConvert.VerifyNCName(value[..colon]))
                    ?? throw new WsdlExportException("QNAME_PREFIX_UNDECLARED");
            return space + local;
        }
        catch (XmlException)
        {
            throw new WsdlExportException("QNAME_INVALID");
        }
    }

    internal static XElement Resolve(Dictionary<XName, XElement> index, XName name) =>
        index.TryGetValue(name, out XElement? element) ? element
            : throw new WsdlExportException("QNAME_UNRESOLVED");
}
