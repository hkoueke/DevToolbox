using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Lit un contrat WSDL et le graphe de documents qu'il atteint, sous la politique en vigueur.
/// </summary>
/// <remarks>
/// Trois interdits ne sont pas des réglages, et ce type ne leur offre aucun moyen de l'être : les
/// définitions de type de document sont refusées, les entités externes ne sont pas résolues, et le budget
/// d'expansion d'entités vaut zéro. Les deux premiers ferment XXE et l'expansion en cascade du même geste,
/// et un réglage capable de les rétablir serait lui-même la vulnérabilité.
/// <para>
/// La résolution locale est ouverte par défaut, parce qu'un WSDL dont les types vivent dans un schéma
/// voisin est le cas ordinaire et non l'exception. Ce qui paie cette ouverture : la racine de confinement,
/// la traversée refusée en la nommant, et l'obligation d'annoncer le périmètre — que
/// <see cref="Core.Wsdl.WsdlContract.ConfinementRoot"/> porte jusqu'au rapport.
/// </para>
/// </remarks>
public sealed class WsdlReader : IWsdlReader
{
    /// <inheritdoc />
    public async Task<WsdlReadResult> ReadAsync(
        string path,
        WsdlLimits limits,
        ImportPolicy policy,
        CancellationToken cancellationToken,
        RetrievalMap? retrieved = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(policy);

        byte[] content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

        return Read(content, path, limits, policy, retrieved ?? RetrievalMap.Empty);
    }

    /// <inheritdoc />
    public WsdlReadResult ReadBytes(
        ReadOnlyMemory<byte> content, string name, WsdlLimits limits, ImportPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(policy);

        return Read(content.ToArray(), name, limits, policy, RetrievalMap.Empty);
    }

    /// <inheritdoc />
    public async Task<bool> IsWsdlAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            FileStream file = File.OpenRead(path);

            await using ConfiguredAsyncDisposable _ = file.ConfigureAwait(false);

            using XmlReader reader = XmlReader.Create(
                file,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0,
                    Async = true,
                });

            // Seul l'élément racine est lu. La question posée est « comment lire ce document », et y
            // répondre ne doit pas coûter de lire le document : un graphe de plusieurs mégaoctets serait
            // alors parcouru deux fois, dont une pour rien.
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                return string.Equals(reader.LocalName, "definitions", StringComparison.Ordinal)
                    && string.Equals(reader.NamespaceURI, WsdlNamespaces.Wsdl11, StringComparison.Ordinal);
            }

            return false;
        }
        catch (XmlException)
        {
            // Pas du XML lisible : ce n'est pas un WSDL, et ce n'est pas à cette question de le refuser.
            // Dire « faux » laisse la lecture OpenAPI s'exprimer, qui sait déjà refuser ce qu'elle lit.
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Lit le document d'entrée, puis le graphe qu'il atteint.</summary>
    private static WsdlReadResult Read(
        byte[] content,
        string name,
        WsdlLimits limits,
        ImportPolicy policy,
        RetrievalMap retrieved)
    {
        List<Diagnostic> diagnostics = [];

        if (!policy.IsCoherent)
        {
            diagnostics.Add(Refusal(
                WsdlDiagnosticCodes.ImportRemoteRefused,
                "La politique autorise la résolution distante sans nommer aucun hôte. Cela n'autorise "
                    + "pas tout : l'outil refuse plutôt que de choisir une lecture charitable."));

            return WsdlReadResult.Refused(diagnostics);
        }

        if (content.LongLength > limits.MaxDocumentBytes)
        {
            diagnostics.Add(Budget("la taille d'un document", limits.MaxDocumentBytes, name));

            return WsdlReadResult.Refused(diagnostics);
        }

        XElement? root = Parse(content, name, diagnostics);

        if (root is null)
        {
            return WsdlReadResult.Refused(diagnostics);
        }

        Definitions? definitions = new DefinitionsReader(name, diagnostics).Read(root);

        return definitions is null
            ? WsdlReadResult.Refused(diagnostics)
            : Assemble(name, content, definitions, limits, policy, retrieved, diagnostics);
    }

    /// <summary>
    /// Analyse le document, définitions de type de document interdites et entités non résolues.
    /// </summary>
    /// <remarks>
    /// L'analyseur lève sur un <c>DOCTYPE</c>, et c'est ce qu'on veut : le refus doit précéder toute
    /// résolution. Un outil qui aurait lu le fichier désigné par une entité externe pour pouvoir le
    /// refuser aurait déjà perdu.
    /// </remarks>
    private static XElement? Parse(byte[] content, string name, List<Diagnostic> diagnostics)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            CloseInput = true,
        };

        try
        {
            using XmlReader reader = XmlReader.Create(new MemoryStream(content), settings);

            return XDocument.Load(reader, LoadOptions.SetLineInfo).Root;
        }
        catch (XmlException failure)
        {
            diagnostics.Add(Mentions(failure, "DTD") || Mentions(failure, "DOCTYPE")
                ? Refusal(
                    WsdlDiagnosticCodes.DocumentTypeDefinition,
                    "« " + Path.GetFileName(name) + " » porte une définition de type de document. Elles "
                        + "ne sont pas traitées, sous aucune configuration : les interdire ferme d'un "
                        + "seul geste les entités externes et leur expansion en cascade, et un réglage "
                        + "capable de les rétablir serait lui-même la faille.",
                    failure)
                : Refusal(
                    WsdlDiagnosticCodes.ParseFailed,
                    "« " + Path.GetFileName(name) + " » n'est pas du XML analysable : " + failure.Message,
                    failure));

            return null;
        }
    }

    private static bool Mentions(XmlException failure, string word) =>
        failure.Message.Contains(word, StringComparison.OrdinalIgnoreCase);

    /// <summary>Assemble le contrat : les schémas, puis les opérations reliées à leurs messages.</summary>
    private static WsdlReadResult Assemble(
        string name,
        byte[] content,
        Definitions definitions,
        WsdlLimits limits,
        ImportPolicy policy,
        RetrievalMap retrieved,
        List<Diagnostic> diagnostics)
    {
        SchemaSetResult schemas = SchemaCompiler.Compile(
            name, definitions, limits, policy, retrieved, diagnostics);

        List<WsdlDocument> documents =
        [
            new WsdlDocument(
                name, WsdlDocumentKind.Definitions, content.LongLength, Hash(content),
                definitions.TargetNamespace),
            .. schemas.Documents,
        ];

        if (documents.Count > limits.MaxDocumentCount)
        {
            diagnostics.Add(Budget("le nombre de documents", limits.MaxDocumentCount, name));

            return WsdlReadResult.Refused(diagnostics);
        }

        if (diagnostics.Any(diagnostic => diagnostic.IsBlocking))
        {
            return WsdlReadResult.Refused(diagnostics);
        }

        return new WsdlReadResult(
            new WsdlContract(
                documents,
                [.. Operations(definitions)],
                schemas.Types,
                definitions.Services,
                schemas.Unresolved,
                policy.ConfinementRoot,
                schemas.Elements),
            diagnostics);
    }

    /// <summary>Relie chaque opération à ses messages et à sa liaison.</summary>
    private static IEnumerable<WsdlOperation> Operations(Definitions definitions)
    {
        Dictionary<string, WsdlMessage> messages = new(StringComparer.Ordinal);

        foreach (WsdlMessage message in definitions.Messages)
        {
            messages[Local(message.Name)] = message;
        }

        foreach (OperationDeclaration declaration in definitions.Operations)
        {
            _ = definitions.Bindings.TryGetValue(declaration.Name, out WsdlBinding? binding);

            yield return new WsdlOperation(
                declaration.Name,
                declaration.Pattern,
                Find(messages, declaration.InputMessage),
                Find(messages, declaration.OutputMessage),
                [.. declaration.FaultMessages.Select(fault => Find(messages, fault)).OfType<WsdlMessage>()],
                binding,
                declaration.Documentation,
                declaration.Location);
        }
    }

    private static WsdlMessage? Find(Dictionary<string, WsdlMessage> messages, string? qualified) =>
        qualified is not null && messages.TryGetValue(Local(qualified), out WsdlMessage? message)
            ? message
            : null;

    /// <summary>Le nom local d'un nom qualifié : ce qui suit le dernier deux-points.</summary>
    private static string Local(string qualified)
    {
        int separator = qualified.LastIndexOf(':');

        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }

    private static string Hash(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    private static Diagnostic Budget(string what, long limit, string document) =>
        Refusal(
            DiagnosticCodes.LimitReached,
            "La lecture de « " + Path.GetFileName(document) + " » a atteint une limite : " + what
                + ", fixée à " + limit.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ". La limite est nommée plutôt que l'échec laissé à deviner.");

    private static Diagnostic Refusal(string code, string message, XmlException? failure = null) =>
        new(
            code,
            DiagnosticSeverity.Error,
            message,
            Position: failure is null
                ? null
                : new SourcePosition(failure.LineNumber, failure.LinePosition));
}
