using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Parcourt les documents WSDL importés, sans effectuer de requête réseau implicite.
/// Les définitions découvertes restent séparées jusqu'à leur liaison par namespace.
/// </summary>
internal sealed class WsdlImportWalker(
    WsdlLimits limits,
    ImportPolicy policy,
    RetrievalMap retrieved,
    List<Diagnostic> diagnostics)
{
    private static readonly XNamespace Wsdl = WsdlNamespaces.Wsdl11;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _namespaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Path, XElement Root, WsdlDocument Document)> _documents = [];

    internal IReadOnlyList<(string Path, XElement Root, WsdlDocument Document)> Documents => _documents;

    internal void Walk(XElement root, string containing)
    {
        _seen.Add(Path.GetFullPath(containing));
        Follow(root, containing, 0);
    }

    private void Follow(XElement root, string containing, int depth)
    {
        foreach (XElement directive in root.Elements(Wsdl + "import"))
        {
            string? location = directive.Attribute("location")?.Value;
            string? targetNamespace = directive.Attribute("namespace")?.Value;
            if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(targetNamespace))
            {
                // The root reader diagnoses root directives. Imported documents are
                // parsed with importsResolved=true and need their own diagnostics.
                if (depth > 0)
                {
                    Refuse(WsdlDiagnosticCodes.ImportUnresolved,
                        "Un wsdl:import doit déclarer namespace et location non vides.", containing);
                }

                continue;
            }

            ProcessImport(containing, depth, location, targetNamespace);

        }
    }


    private void ProcessImport(string containing, int depth, string location, string targetNamespace)
    {
        if (depth >= limits.MaxImportChainDepth)
        {
            Refuse(DiagnosticCodes.LimitReached, "Limite de profondeur des wsdl:import atteinte.", containing);
            return;
        }

        string? path = Resolve(containing, location);
        if (path is null)
        {
            return;
        }

        if (!_seen.Add(path))
        {
            AlreadySeen(path, targetNamespace);
            return;
        }

        if (_documents.Count + 2 > limits.MaxDocumentCount)
        {
            Refuse(DiagnosticCodes.LimitReached, "Limite de documents WSDL atteinte.", path);
            return;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException failure)
        {
            Refuse(WsdlDiagnosticCodes.ImportUnresolved, failure.Message, path);
            return;
        }

        if (bytes.LongLength > limits.MaxDocumentBytes)
        {
            Refuse(DiagnosticCodes.LimitReached, "Document WSDL importé trop volumineux.", path);
            return;
        }

        XElement? imported = Load(path, bytes);
        if (imported is null)
        {
            return;
        }

        if (ValidateImported(imported, path, targetNamespace))
        {
            _namespaces[path] = targetNamespace;
            Register(path, imported, bytes, targetNamespace, depth);
        }
    }

    private void AlreadySeen(string path, string targetNamespace)
    {
        if (_namespaces.TryGetValue(path, out string? previous)
            && !string.Equals(previous, targetNamespace, StringComparison.Ordinal))
        {
            Refuse(WsdlDiagnosticCodes.StructuralViolation,
                "Un même WSDL est importé sous deux namespaces différents : "
                    + previous + " et " + targetNamespace, path);
            return;
        }

        diagnostics.Add(new Diagnostic(WsdlDiagnosticCodes.ImportCycle,
            DiagnosticSeverity.Information, "Import WSDL déjà parcouru : " + Path.GetFileName(path)));
    }

    private bool ValidateImported(XElement imported, string path, string targetNamespace)
    {
        if (ExceedsDepth(imported))
        {
            Refuse(DiagnosticCodes.LimitReached,
                "Limite de profondeur d'imbrication du document WSDL importé atteinte.", path);
            return false;
        }

        if (!string.Equals(imported.Attribute("targetNamespace")?.Value,
                targetNamespace, StringComparison.Ordinal))
        {
            Refuse(WsdlDiagnosticCodes.StructuralViolation,
                "Le targetNamespace du WSDL importé ne correspond pas au namespace déclaré.", path);
            return false;
        }

        return true;
    }

    private bool ExceedsDepth(XElement root)
    {
        Queue<(XElement Element, int Depth)> pending = new();
        pending.Enqueue((root, 1));
        while (pending.Count > 0)
        {
            (XElement element, int depth) = pending.Dequeue();
            if (depth > limits.MaxNestingDepth)
            {
                return true;
            }

            foreach (XElement child in element.Elements())
            {
                pending.Enqueue((child, depth + 1));
            }
        }

        return false;
    }

    private void Register(string path, XElement imported, byte[] bytes, string targetNamespace, int depth)
    {
        _documents.Add((path, imported, new WsdlDocument(
            path, WsdlDocumentKind.Definitions, bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)), targetNamespace)));
        Follow(imported, path, depth + 1);
    }

    private XElement? Load(string path, byte[] bytes)
    {
        try
        {
            using XmlReader reader = XmlReader.Create(new MemoryStream(bytes),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0,
                });
            XElement? root = XDocument.Load(reader, LoadOptions.SetLineInfo).Root;
            if (root?.Name == Wsdl + "definitions")
            {
                return root;
            }

            Refuse(WsdlDiagnosticCodes.StructuralViolation,
                "L'import ne contient pas de racine wsdl:definitions WSDL 1.1.", path);
        }
        catch (XmlException failure)
        {
            Refuse(WsdlDiagnosticCodes.ParseFailed, failure.Message, path);
        }

        return null;
    }

    private string? Resolve(string containing, string location)
    {
        if (retrieved.Origin(containing) is { } origin)
        {
            if (Uri.TryCreate(origin, location, out Uri? remote)
                && retrieved.Local(remote) is { } materialized)
            {
                string local = Path.GetFullPath(materialized);
                if (!InsideRoot(local))
                {
                    Refuse(WsdlDiagnosticCodes.ImportOutsideRoot,
                        "Import WSDL matérialisé hors de la racine de confinement : " + location, containing);
                    return null;
                }

                return local;
            }

            Refuse(WsdlDiagnosticCodes.ImportUnresolved,
                "Import WSDL distant non matérialisé : " + location, containing);
            return null;
        }

        if (Uri.TryCreate(location, UriKind.Absolute, out Uri? absolute) && !absolute.IsFile)
        {
            Refuse(policy.AllowRemote
                    ? WsdlDiagnosticCodes.ImportUnresolved
                    : WsdlDiagnosticCodes.ImportRemoteRefused,
                "Import WSDL distant non matérialisé : " + location, containing);
            return null;
        }

        string candidate = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(containing)) ?? ".", location));
        if (!InsideRoot(candidate))
        {
            Refuse(WsdlDiagnosticCodes.ImportOutsideRoot,
                "Import WSDL hors de la racine de confinement : " + location, containing);
            return null;
        }

        if (!File.Exists(candidate))
        {
            Refuse(WsdlDiagnosticCodes.ImportUnresolved,
                "Import WSDL introuvable : " + location, containing);
            return null;
        }

        return candidate;
    }

    private bool InsideRoot(string candidate)
    {
        string root = Path.GetFullPath(policy.ConfinementRoot);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root : root + Path.DirectorySeparatorChar;
        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void Refuse(string code, string message, string document) =>
        diagnostics.Add(new Diagnostic(code, DiagnosticSeverity.Error,
            message + " (" + Path.GetFileName(document) + ")"));
}
