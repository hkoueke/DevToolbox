using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Cherche deux déclarations du même nom dans un même espace de noms, et nomme les deux documents.
/// </summary>
/// <remarks>
/// Avant le compilateur, et non après : <c>XmlSchemaSet</c> détecte bien le conflit, mais sa plainte ne
/// nomme qu'un seul fichier — celui qu'il lisait quand il s'en est aperçu. Cela envoie corriger un
/// document qui n'a peut-être rien à se reprocher, l'autre étant le fautif. Comme l'outil connaît déjà
/// tous les documents qu'il a ouverts, il peut dire les deux, et c'est la seule raison d'écrire ce
/// contrôle à la main.
/// <para>
/// Répartir un espace de noms sur plusieurs fichiers est licite et courant, et n'est pas signalé : seul
/// un nom déclaré deux fois l'est.
/// </para>
/// </remarks>
internal static class SchemaConflicts
{
    private static readonly XNamespace Xsd = WsdlNamespaces.Schema;

    private static readonly string[] Declarations =
        ["simpleType", "complexType", "element", "attribute", "group", "attributeGroup"];

    /// <summary>Signale chaque nom déclaré deux fois dans un même espace de noms.</summary>
    /// <param name="document">Le document d'entrée.</param>
    /// <param name="definitions">Ce qu'il déclare, dont ses schémas en ligne.</param>
    /// <param name="read">Les schémas lus depuis le disque.</param>
    /// <param name="diagnostics">Où consigner ce qu'il y a à dire.</param>
    internal static void Report(
        string document,
        Definitions definitions,
        IReadOnlyList<ReadSchema> read,
        List<Diagnostic> diagnostics)
    {
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (ReadSchema schema in All(document, definitions, read))
        {
            foreach (XElement declaration in Named(schema.Element))
            {
                Check(schema, declaration, seen, diagnostics);
            }
        }
    }

    /// <summary>Les schémas en ligne et ceux du disque, sous une même forme.</summary>
    private static IEnumerable<ReadSchema> All(
        string document, Definitions definitions, IReadOnlyList<ReadSchema> read) =>
        definitions.InlineSchemas
            .Select(schema => new ReadSchema(document, schema.Element))
            .Concat(read);

    private static IEnumerable<XElement> Named(XElement schema) =>
        schema.Elements()
            .Where(child =>
                Declarations.Contains(child.Name.LocalName, StringComparer.Ordinal)
                && child.Name.Namespace == Xsd
                && child.Attribute("name") is not null);

    private static void Check(
        ReadSchema schema,
        XElement declaration,
        Dictionary<string, string> seen,
        List<Diagnostic> diagnostics)
    {
        string space = schema.Element.Attribute("targetNamespace")?.Value ?? string.Empty;
        string key = space + " " + declaration.Name.LocalName + " " + declaration.Attribute("name")!.Value;

        if (seen.TryAdd(key, schema.Document))
        {
            return;
        }

        string first = seen[key];

        if (string.Equals(first, schema.Document, StringComparison.OrdinalIgnoreCase))
        {
            // Deux fois dans le même fichier : le compilateur le dira mieux, avec sa position exacte.
            return;
        }

        diagnostics.Add(new Diagnostic(
            WsdlDiagnosticCodes.SchemaConflict,
            DiagnosticSeverity.Error,
            "« " + declaration.Attribute("name")!.Value + " » est déclaré deux fois dans l'espace de noms "
                + "« " + space + " » : une fois par « " + Path.GetFileName(first) + " », une fois par « "
                + Path.GetFileName(schema.Document) + " ». Les deux sont nommés parce que l'outil ne peut "
                + "pas dire lequel est le fautif.",
            Position: null));
    }
}
