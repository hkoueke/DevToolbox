using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Suit les imports d'un graphe de schémas, et s'arrête où la politique le dit.
/// </summary>
/// <remarks>
/// Trois refus, chacun nommé plutôt que laissé à deviner : un chemin qui quitterait la racine de
/// confinement, un emplacement distant quand la politique le ferme, et un <c>xsd:redefine</c> — que
/// l'outil refuse au lieu de le lire et de l'ignorer, parce qu'ignorer un redefine change le sens du
/// document sans le dire.
/// <para>
/// Un cycle est consigné et non suivi : XSD l'autorise, et le refuser serait aussi faux que boucler. La
/// profondeur de chaîne et le nombre de documents sont des budgets nommés, parce que la tranche
/// précédente a appris qu'une forme plausible peut épuiser une pile sans rien dire de ce qu'elle lisait.
/// </para>
/// </remarks>
internal sealed class ImportWalker
{
    private static readonly XNamespace Xsd = WsdlNamespaces.Schema;

    private readonly WsdlLimits _limits;
    private readonly ImportPolicy _policy;
    private readonly RetrievalMap _retrieved;
    private readonly List<Diagnostic> _diagnostics;
    private readonly Dictionary<string, string> _resolved = new(StringComparer.Ordinal);
    private readonly List<WsdlDocument> _documents = [];
    private readonly List<ReadSchema> _schemas = [];
    private readonly List<UnresolvedImport> _unresolved = [];
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Crée le parcours.</summary>
    /// <param name="limits">Le budget.</param>
    /// <param name="policy">Ce qu'on s'autorise à résoudre.</param>
    /// <param name="retrieved">D'où vient chaque document, s'ils ont été récupérés.</param>
    /// <param name="diagnostics">Où consigner ce qu'il y a à dire.</param>
    internal ImportWalker(
        WsdlLimits limits,
        ImportPolicy policy,
        RetrievalMap retrieved,
        List<Diagnostic> diagnostics)
    {
        _limits = limits;
        _policy = policy;
        _retrieved = retrieved;
        _diagnostics = diagnostics;
    }

    /// <summary>Les emplacements autorisés, et le fichier qui répond pour chacun.</summary>
    internal IReadOnlyDictionary<string, string> Resolved => _resolved;

    /// <summary>Les schémas effectivement lus.</summary>
    internal IReadOnlyList<WsdlDocument> Documents => _documents;

    /// <summary>Les schémas lus, avec leur contenu : le conflit a besoin de les relire.</summary>
    internal IReadOnlyList<ReadSchema> Schemas => _schemas;

    /// <summary>Les imports que la politique n'a pas laissé résoudre.</summary>
    internal IReadOnlyList<UnresolvedImport> Unresolved => _unresolved;

    /// <summary>Suit les imports d'un schéma, et de ceux qu'il atteint.</summary>
    /// <param name="schema">L'élément <c>xs:schema</c>.</param>
    /// <param name="containing">Le document qui le porte, pour résoudre le relatif.</param>
    internal void Walk(XElement schema, string containing) => Walk(schema, containing, 0);

    private void Walk(XElement schema, string containing, int depth)
    {
        if (depth > _limits.MaxImportChainDepth)
        {
            _diagnostics.Add(Budget(
                "la longueur d'une chaîne d'imports", _limits.MaxImportChainDepth, containing));

            return;
        }

        foreach (XElement directive in Directives(schema))
        {
            Follow(directive, containing, depth);
        }
    }

    private static IEnumerable<XElement> Directives(XElement schema) =>
        schema.Elements().Where(child =>
            child.Name == Xsd + "import"
            || child.Name == Xsd + "include"
            || child.Name == Xsd + "redefine");

    /// <summary>Décide d'un import : le suivre, ou le refuser en nommant pourquoi.</summary>
    private void Follow(XElement directive, string containing, int depth)
    {
        string where = directive.Attribute("schemaLocation")?.Value ?? string.Empty;
        string targeted = directive.Attribute("namespace")?.Value ?? string.Empty;
        WsdlLocation location = At(directive, containing);

        if (directive.Name == Xsd + "redefine")
        {
            Decline(
                targeted, where, location,
                "Un « xsd:redefine » change le sens d'un document déjà lu. L'outil le refuse plutôt que "
                    + "de le lire et de l'ignorer en silence.",
                WsdlDiagnosticCodes.Redefine);

            return;
        }

        if (string.IsNullOrWhiteSpace(where))
        {
            // Un import sans emplacement est licite : il déclare une dépendance que le jeu de schémas
            // doit déjà porter. Ce n'est pas un refus, c'est une absence de travail.
            return;
        }

        Resolve(where, targeted, location, containing, depth);
    }

    private void Resolve(
        string where, string targeted, WsdlLocation location, string containing, int depth)
    {
        if (_retrieved.Origin(containing) is { } origin)
        {
            Retrieved(where, targeted, location, containing, origin, depth);

            return;
        }

        if (Uri.TryCreate(where, UriKind.Absolute, out Uri? absolute) && !absolute.IsFile)
        {
            Remote(absolute, targeted, where, location);

            return;
        }

        Local(where, targeted, location, containing, depth);
    }

    /// <summary>
    /// Résout l'import d'un document récupéré contre l'adresse d'où ce document vient.
    /// </summary>
    /// <remarks>
    /// Contre l'adresse, et jamais contre le dossier où il a été écrit. Matérialiser change l'endroit où
    /// un document se trouve ; cela ne doit pas changer ce que ses imports désignent. Un
    /// <c>../commun/types.xsd</c> écrit par un document servi depuis <c>/contrats/v2/</c> désigne
    /// <c>/contrats/commun/types.xsd</c> sur le serveur, ce que le dossier à plat ne dirait plus.
    /// </remarks>
    private void Retrieved(
        string where,
        string targeted,
        WsdlLocation location,
        string containing,
        Uri origin,
        int depth)
    {
        if (!Uri.TryCreate(origin, where, out Uri? designated))
        {
            Decline(
                targeted, where, location,
                "Cet emplacement ne se résout pas contre l'adresse du document qui l'écrit.",
                WsdlDiagnosticCodes.ImportUnresolved);

            return;
        }

        if (_retrieved.Local(designated) is not { } materialised)
        {
            Decline(
                targeted, where, location,
                "Ce document désigne « " + RetrievedOrigin.From(designated, default, []).RedactedUrl
                    + " », qui n'a pas été récupéré. Un graphe à moitié matérialisé se lirait comme un "
                    + "contrat à moitié écrit.",
                WsdlDiagnosticCodes.ImportUnresolved);

            return;
        }

        Accept(
            materialised, Path.GetFileName(materialised),
            Path.GetDirectoryName(Path.GetFullPath(materialised)) ?? ".", containing, location, depth);
    }

    /// <summary>Résout l'import d'un document donné par chemin, contre son dossier.</summary>
    private void Local(
        string where, string targeted, WsdlLocation location, string containing, int depth)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(containing)) ?? ".";
        string candidate = Path.GetFullPath(Path.Combine(directory, where));

        if (!Inside(candidate))
        {
            Decline(
                targeted, where, location,
                "Ce chemin quitte la racine à laquelle la résolution est confinée, « "
                    + _policy.ConfinementRoot + " ».",
                WsdlDiagnosticCodes.ImportOutsideRoot);

            return;
        }

        if (!File.Exists(candidate))
        {
            Decline(
                targeted, where, location,
                "Aucun document ne se trouve à cet emplacement.",
                WsdlDiagnosticCodes.ImportUnresolved);

            return;
        }

        Accept(candidate, where, directory, containing, location, depth);
    }

    /// <summary>Retient un document, puis suit ce qu'il importe à son tour.</summary>
    private void Accept(
        string path,
        string where,
        string directory,
        string containing,
        WsdlLocation location,
        int depth)
    {
        if (!_seen.Add(path))
        {
            // Déjà lu : c'est un cycle, et XSD l'autorise. Consigné une fois, jamais suivi deux fois.
            _diagnostics.Add(new Diagnostic(
                WsdlDiagnosticCodes.ImportCycle,
                DiagnosticSeverity.Information,
                "« " + Path.GetFileName(path) + " » est atteint une seconde fois : un cycle d'imports, "
                    + "légal, consigné et non suivi. (" + location.Display + ")"));

            return;
        }

        if (_documents.Count >= _limits.MaxDocumentCount)
        {
            _diagnostics.Add(Budget(
                "le nombre de documents d'un graphe", _limits.MaxDocumentCount, containing));

            return;
        }

        byte[] content = File.ReadAllBytes(path);

        if (content.LongLength > _limits.MaxDocumentBytes)
        {
            _diagnostics.Add(Budget("la taille d'un document", _limits.MaxDocumentBytes, path));

            return;
        }

        XElement? schema = Load(path, content);

        if (schema is null)
        {
            return;
        }

        _resolved[new Uri(Path.Combine(directory, where)).ToString()] = path;
        _resolved[new Uri(path).ToString()] = path;

        _documents.Add(new WsdlDocument(
            path, WsdlDocumentKind.Schema, content.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(content)),
            schema.Attribute("targetNamespace")?.Value));

        _schemas.Add(new ReadSchema(path, schema));

        Walk(schema, path, depth + 1);
    }

    /// <summary>Lit un schéma, définitions de type de document interdites.</summary>
    private XElement? Load(string path, byte[] content)
    {
        try
        {
            using XmlReader reader = XmlReader.Create(
                new MemoryStream(content),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0,
                });

            return XDocument.Load(reader, LoadOptions.SetLineInfo).Root;
        }
        catch (XmlException failure)
        {
            _diagnostics.Add(new Diagnostic(
                WsdlDiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "« " + Path.GetFileName(path) + " » ne se lit pas : " + failure.Message,
                Position: new Core.Syntax.SourcePosition(failure.LineNumber, failure.LinePosition)));

            return null;
        }
    }

    private void Remote(Uri absolute, string targeted, string where, WsdlLocation location)
    {
        if (!_policy.AllowRemote)
        {
            Decline(
                targeted, where, location,
                "La résolution distante est fermée. L'activer demande de nommer les hôtes autorisés.",
                WsdlDiagnosticCodes.ImportRemoteRefused);

            return;
        }

        if (!_policy.AllowedHosts.Contains(absolute.Host, StringComparer.OrdinalIgnoreCase))
        {
            Decline(
                targeted, where, location,
                "L'hôte « " + absolute.Host + " » ne figure pas parmi les hôtes autorisés.",
                WsdlDiagnosticCodes.ImportRemoteRefused);

            return;
        }

        // La récupération elle-même n'a pas lieu ici : elle appartient à la moitié URL de cette tranche,
        // qui matérialise avant de lire. Dire ce qui manque vaut mieux que le taire.
        Decline(
            targeted, where, location,
            "Cet emplacement est autorisé mais n'a pas été récupéré : donner le contrat par son adresse "
                + "fait matérialiser son graphe avant la lecture.",
            WsdlDiagnosticCodes.ImportUnresolved);
    }

    private bool Inside(string candidate) =>
        Path.GetFullPath(candidate)
            .StartsWith(Path.GetFullPath(_policy.ConfinementRoot), StringComparison.OrdinalIgnoreCase);

    private void Decline(
        string targeted, string where, WsdlLocation location, string why, string code)
    {
        _unresolved.Add(new UnresolvedImport(targeted, where, location, why));

        _diagnostics.Add(new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            "« " + where + " » n'a pas été résolu : " + why + " (" + location.Display + ")",
            Position: new Core.Syntax.SourcePosition(location.Line, location.Column)));
    }

    private static WsdlLocation At(XElement element, string containing)
    {
        IXmlLineInfo info = element;

        return new WsdlLocation(
            containing,
            "schema/" + element.Name.LocalName,
            info.HasLineInfo() ? info.LineNumber : 0,
            info.HasLineInfo() ? info.LinePosition : 0);
    }

    private static Diagnostic Budget(string what, long limit, string document) =>
        new(
            DiagnosticCodes.LimitReached,
            DiagnosticSeverity.Error,
            "La lecture de « " + Path.GetFileName(document) + " » a atteint une limite : " + what
                + ", fixée à " + limit.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
}
