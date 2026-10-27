using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Lit la structure d'un document <c>wsdl:definitions</c> : messages, types de port, liaisons, services.
/// </summary>
/// <remarks>
/// À la main, et non par un modèle d'objets. La structure de WSDL 1.1 tient en une page — six sortes
/// d'éléments de premier niveau — et la lire soi-même coûte moins que d'adapter un modèle général, tout
/// en donnant la ligne et la colonne de chaque chose par <see cref="IXmlLineInfo"/>. Le seul modèle que
/// .NET ait jamais offert pour cela, celui de WCF, a disparu, et il résolvait ses imports à ses propres
/// conditions.
/// <para>
/// La moitié XSD est l'inverse : particules, dérivation, groupes de substitution et facettes forment un
/// vrai système de types, et le compilateur de schémas s'en charge.
/// </para>
/// </remarks>
internal sealed class DefinitionsReader
{
    private static readonly XNamespace Wsdl = WsdlNamespaces.Wsdl11;

    private readonly string _document;
    private readonly List<Diagnostic> _diagnostics;

    /// <summary>Crée le lecteur pour un document donné.</summary>
    /// <param name="document">Le chemin du document, pour les emplacements.</param>
    /// <param name="diagnostics">Où consigner ce qu'il y a à dire.</param>
    internal DefinitionsReader(string document, List<Diagnostic> diagnostics)
    {
        _document = document;
        _diagnostics = diagnostics;
    }

    /// <summary>Lit la structure, ou rend <see langword="null"/> si elle n'est pas lisible.</summary>
    /// <param name="root">La racine du document.</param>
    /// <returns>Ce que le document déclare, ou rien.</returns>
    internal Definitions? Read(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Name != Wsdl + "definitions")
        {
            Refuse(root, Unsupported(root.Name.NamespaceName));

            return null;
        }

        if (Imported(root) is { } imported)
        {
            RefuseImport(imported);

            return null;
        }

        return new Definitions(
            root.Attribute("targetNamespace")?.Value,
            [.. Messages(root)],
            [.. Operations(root)],
            Bindings(root),
            [.. Services(root)],
            [.. InlineSchemas(root)]);
    }

    /// <summary>
    /// Le premier <c>wsdl:import</c> du document, s'il y en a un.
    /// </summary>
    /// <remarks>
    /// Distinct d'un <c>xs:import</c>, et c'est tout le piège : celui-ci répartit les messages, les types
    /// de port et les liaisons sur plusieurs documents WSDL, là où l'autre ne répartit que des schémas.
    /// Le suiveur d'imports ne descend que dans les <c>xs:schema</c>, si bien qu'un <c>wsdl:import</c>
    /// n'était ni suivi ni refusé : le contrat était bâti sur le seul document d'entrée, ses opérations
    /// importées disparaissaient sans un mot, et la lecture se déclarait fidèle.
    /// </remarks>
    private static XElement? Imported(XElement root) =>
        root.Elements(Wsdl + "import").FirstOrDefault();

    /// <summary>
    /// Le message de refus d'un contrat réparti sur plusieurs documents WSDL.
    /// </summary>
    /// <remarks>
    /// Refusé, et non lu à moitié. Un contrat dont les opérations importées manquent se comparerait
    /// contre une production en annonçant absentes des routes que le service expose bel et bien — et la
    /// consolidation en tirerait des conclusions sur un contrat que personne ne lui a donné en entier.
    /// Suivre un <c>wsdl:import</c> est une fonctionnalité ; le refuser en le nommant est la seule
    /// réponse honnête en attendant.
    /// </remarks>
    private static string Split(XElement imported) =>
        "Ce contrat est réparti sur plusieurs documents WSDL : il porte un « wsdl:import » de « "
        + (imported.Attribute("namespace")?.Value ?? "(sans espace de noms)")
        + " », à « " + (imported.Attribute("location")?.Value ?? "(sans emplacement)")
        + " ». L'outil ne suit pas encore un « wsdl:import » — distinct d'un « xs:import », qui répartit "
        + "des schémas et qui lui est suivi. Le lire sans l'import donnerait un contrat amputé de ses "
        + "opérations importées, et le dirait lu fidèlement : il est donc refusé.";

    /// <summary>Le message de refus d'une version non prise en charge.</summary>
    private static string Unsupported(string observed) =>
        string.Equals(observed, WsdlNamespaces.Wsdl20, StringComparison.Ordinal)
            ? "Ce document déclare WSDL 2.0. Seul WSDL 1.1 est pris en charge : lire une version selon "
                + "les règles d'une autre reviendrait à décrire un contrat autrement qu'il n'est écrit."
            : "La racine de ce document n'est pas un « wsdl:definitions » de WSDL 1.1.";

    private IEnumerable<WsdlMessage> Messages(XElement root) =>
        root.Elements(Wsdl + "message")
            .Select(message => new WsdlMessage(
                message.Attribute("name")?.Value ?? "(sans nom)",
                [.. message.Elements(Wsdl + "part").Select(Part)],
                At(message, "message[" + (message.Attribute("name")?.Value ?? "?") + "]")));

    private WsdlPart Part(XElement part) => new(
        part.Attribute("name")?.Value ?? "(sans nom)",
        Qualified(part, part.Attribute("element")?.Value),
        Qualified(part, part.Attribute("type")?.Value),
        At(part, "part[" + (part.Attribute("name")?.Value ?? "?") + "]"));

    /// <summary>
    /// Résout le préfixe d'un nom qualifié en l'espace de noms qu'il désigne.
    /// </summary>
    /// <param name="carrier">L'élément qui porte le nom, et dont les déclarations sont en portée.</param>
    /// <param name="value">Le nom tel qu'il est écrit, préfixe compris.</param>
    /// <returns>« espace:local », ou le nom local si aucun espace de noms ne s'applique.</returns>
    /// <remarks>
    /// Résolu ici, une fois, et jamais plus bas. Un préfixe est une commodité d'écriture locale au
    /// document : deux livraisons du même contrat peuvent le choisir différemment, et tout ce qui
    /// comparerait des noms préfixés les verrait différer. La forme produite est celle que
    /// <c>XmlQualifiedName.ToString()</c> donne, afin qu'un nom lu dans le WSDL et un nom
    /// lu dans le jeu de schémas compilé soient la même chaîne.
    /// <para>
    /// Le repli sur le nom local n'est pas une négligence : un nom sans préfixe, dans un document sans
    /// espace de noms par défaut, <em>est</em> un nom local, et en inventer un espace de noms dirait plus
    /// que le document.
    /// </para>
    /// </remarks>
    private static string? Qualified(XElement carrier, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        int colon = value.IndexOf(':', StringComparison.Ordinal);
        string local = colon < 0 ? value : value[(colon + 1)..];

        XNamespace? space = colon < 0
            ? carrier.GetDefaultNamespace()
            : carrier.GetNamespaceOfPrefix(value[..colon]);

        return space is null || space == XNamespace.None
            ? local
            : space.NamespaceName + ":" + local;
    }

    /// <summary>Le message qu'un flux désigne, son préfixe résolu.</summary>
    private static string? Message(XElement? flow) =>
        flow is null ? null : Qualified(flow, flow.Attribute("message")?.Value);

    /// <summary>Les opérations de tous les types de port, avec leur schéma d'échange.</summary>
    private IEnumerable<OperationDeclaration> Operations(XElement root) =>
        root.Elements(Wsdl + "portType")
            .SelectMany(portType => portType.Elements(Wsdl + "operation")
                .Select(operation => Declare(portType, operation)));

    private OperationDeclaration Declare(XElement portType, XElement operation)
    {
        string name = operation.Attribute("name")?.Value ?? "(sans nom)";

        // L'ordre des éléments décide du schéma d'échange : c'est la seule chose qui distingue une
        // sollicitation-réponse d'une requête-réponse, et la supposer ferait projeter une réponse à une
        // opération qui n'en rend aucune.
        List<XElement> flow =
        [
            .. operation.Elements()
                .Where(child => child.Name == Wsdl + "input" || child.Name == Wsdl + "output"),
        ];

        return new OperationDeclaration(
            name,
            Pattern(flow),
            Message(flow.FirstOrDefault(child => child.Name == Wsdl + "input")),
            Message(flow.FirstOrDefault(child => child.Name == Wsdl + "output")),
            [
                .. operation.Elements(Wsdl + "fault")
                    .Select(Message)
                    .OfType<string>(),
            ],
            Text(operation, "documentation"),
            At(
                operation,
                "portType[" + (portType.Attribute("name")?.Value ?? "?") + "]/operation[" + name + "]"));
    }

    /// <summary>Le schéma d'échange, d'après l'ordre et la présence des deux messages.</summary>
    private static MessagePattern Pattern(List<XElement> flow)
    {
        bool first = flow.Count > 0 && flow[0].Name == Wsdl + "input";

        return flow.Count switch
        {
            0 => MessagePattern.OneWay,
            1 => first ? MessagePattern.OneWay : MessagePattern.Notification,
            _ => first ? MessagePattern.RequestResponse : MessagePattern.SolicitResponse,
        };
    }

    /// <summary>Les liaisons, par nom d'opération et par nom de liaison.</summary>
    private Dictionary<string, WsdlBinding> Bindings(XElement root)
    {
        Dictionary<string, WsdlBinding> bindings = new(StringComparer.Ordinal);

        foreach (XElement binding in root.Elements(Wsdl + "binding"))
        {
            string name = binding.Attribute("name")?.Value ?? "(sans nom)";
            XElement? soap = binding.Elements()
                .FirstOrDefault(child => string.Equals(child.Name.LocalName, "binding", StringComparison.Ordinal) && IsSoap(child));

            foreach (XElement operation in binding.Elements(Wsdl + "operation"))
            {
                string key = operation.Attribute("name")?.Value ?? "(sans nom)";

                bindings[key] = Describe(name, binding, soap, operation);
            }
        }

        return bindings;
    }

    private WsdlBinding Describe(
        string name, XElement binding, XElement? soap, XElement operation)
    {
        XElement? body = operation.Elements(Wsdl + "input")
            .Elements()
            .FirstOrDefault(child => string.Equals(child.Name.LocalName, "body", StringComparison.Ordinal));

        XElement? action = operation.Elements()
            .FirstOrDefault(child => string.Equals(child.Name.LocalName, "operation", StringComparison.Ordinal) && IsSoap(child));

        return new WsdlBinding(
            name,
            soap?.Attribute("style")?.Value ?? "document",
            body?.Attribute("use")?.Value ?? "literal",
            soap?.Attribute("transport")?.Value,
            Version(soap),
            action?.Attribute("soapAction")?.Value,
            At(binding, "binding[" + name + "]"));
    }

    private static bool IsSoap(XElement element) =>
        string.Equals(element.Name.NamespaceName, WsdlNamespaces.Soap11, StringComparison.Ordinal)
        || string.Equals(element.Name.NamespaceName, WsdlNamespaces.Soap12, StringComparison.Ordinal);

    private static SoapVersion Version(XElement? soap) => soap?.Name.NamespaceName switch
    {
        WsdlNamespaces.Soap11 => SoapVersion.Soap11,
        WsdlNamespaces.Soap12 => SoapVersion.Soap12,
        _ => SoapVersion.None,
    };

    private IEnumerable<WsdlService> Services(XElement root) =>
        root.Elements(Wsdl + "service")
            .Select(service => new WsdlService(
                service.Attribute("name")?.Value ?? "(sans nom)",
                [
                    .. service.Elements(Wsdl + "port").Select(port => new WsdlPort(
                        port.Attribute("name")?.Value ?? "(sans nom)",
                        port.Attribute("binding")?.Value ?? string.Empty,
                        At(port, "port[" + (port.Attribute("name")?.Value ?? "?") + "]"))),
                ],
                At(service, "service[" + (service.Attribute("name")?.Value ?? "?") + "]")));

    /// <summary>Les schémas écrits à l'intérieur de <c>wsdl:types</c>.</summary>
    private IEnumerable<InlineSchema> InlineSchemas(XElement root) =>
        root.Elements(Wsdl + "types")
            .Elements(XNamespace.Get(WsdlNamespaces.Schema) + "schema")
            .Select(schema => new InlineSchema(
                schema.Attribute("targetNamespace")?.Value,
                schema,
                At(schema, "types/schema")));

    private static string? Text(XElement element, string localName) =>
        element.Elements(Wsdl + localName).FirstOrDefault()?.Value.Trim() is { Length: > 0 } text
            ? text
            : null;

    private WsdlLocation At(XElement element, string path)
    {
        IXmlLineInfo info = element;

        return new WsdlLocation(
            _document, path, info.HasLineInfo() ? info.LineNumber : 0,
            info.HasLineInfo() ? info.LinePosition : 0);
    }

    /// <summary>Refuse un contrat réparti, sous le code qui dit qu'un import n'a pas été résolu.</summary>
    private void RefuseImport(XElement imported) =>
        _diagnostics.Add(new Diagnostic(
            WsdlDiagnosticCodes.ImportUnresolved,
            DiagnosticSeverity.Error,
            Split(imported) + " (" + At(imported, "import").Display + ")",
            Position: new Core.Syntax.SourcePosition(
                ((IXmlLineInfo)imported).LineNumber, ((IXmlLineInfo)imported).LinePosition)));

    private void Refuse(XElement root, string message) =>
        _diagnostics.Add(new Diagnostic(
            string.Equals(root.Name.NamespaceName, WsdlNamespaces.Wsdl20, StringComparison.Ordinal)
                ? WsdlDiagnosticCodes.UnsupportedVersion
                : WsdlDiagnosticCodes.StructuralViolation,
            DiagnosticSeverity.Error,
            message + " (" + At(root, "definitions").Display + ")",
            Position: new Core.Syntax.SourcePosition(
                ((IXmlLineInfo)root).LineNumber, ((IXmlLineInfo)root).LinePosition)));
}
