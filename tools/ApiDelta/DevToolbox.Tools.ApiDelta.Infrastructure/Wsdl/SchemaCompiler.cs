using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Compile le jeu de schémas d'un contrat, en ne résolvant que ce que la politique autorise.
/// </summary>
/// <remarks>
/// Un point mesuré plutôt que supposé, et l'hypothèse de départ était inversée : <see
/// cref="XmlSchemaSet.XmlResolver"/> vaut <see langword="null"/> par défaut depuis .NET Core, si bien que
/// sans résolveur il ne suit <em>aucun</em> <c>schemaLocation</c>. La posture est donc fermée d'origine, et
/// le travail consiste à fournir un résolveur contrôlé — non à brider un résolveur trop zélé, comme le
/// plan l'avait d'abord écrit.
/// <para>
/// Le résolveur posé ici n'ouvre que des fichiers déjà vérifiés sous la racine de confinement. Un import
/// distant, ou sortant de la racine, est refusé <em>avant</em> d'arriver au compilateur : le refus nomme
/// le chemin plutôt que d'expirer sur une tentative de connexion.
/// </para>
/// </remarks>
internal static class SchemaCompiler
{
    /// <summary>Compile les schémas en ligne et ceux qu'ils importent.</summary>
    /// <param name="document">Le document d'entrée, pour les emplacements.</param>
    /// <param name="definitions">Ce que le document déclare.</param>
    /// <param name="limits">Le budget.</param>
    /// <param name="policy">Ce qu'on s'autorise à résoudre.</param>
    /// <param name="retrieved">D'où vient chaque document, s'ils ont été récupérés.</param>
    /// <param name="diagnostics">Où consigner ce qu'il y a à dire.</param>
    /// <returns>Les types compilés, les documents lus, et ce qui est resté non résolu.</returns>
    internal static SchemaSetResult Compile(
        string document,
        Definitions definitions,
        WsdlLimits limits,
        ImportPolicy policy,
        RetrievalMap retrieved,
        List<Diagnostic> diagnostics)
    {
        ImportWalker walker = new(limits, policy, retrieved, diagnostics);

        foreach (InlineSchema schema in definitions.InlineSchemas)
        {
            walker.Walk(schema.Element, document);
        }

        XmlSchemaSet set = new()
        {
            // Ne répond que depuis l'ensemble déjà vérifié. Sans cela le compilateur ne suivrait rien du
            // tout, ce qui serait sûr mais ne lirait aucun contrat réel.
            XmlResolver = new ConfinedResolver(walker.Resolved),
        };

        set.ValidationEventHandler += (_, argument) => diagnostics.Add(Complaint(argument, document));

        foreach (InlineSchema schema in definitions.InlineSchemas)
        {
            Add(set, schema, document, diagnostics);
        }

        foreach (string path in walker.Resolved.Values)
        {
            Add(set, path, document, diagnostics);
        }

        SchemaConflicts.Report(document, definitions, walker.Schemas, diagnostics);

        return Finish(set, walker, diagnostics, document);
    }

    /// <summary>Compile, puis relève les types sous une forme que la projection sait lire.</summary>
    private static SchemaSetResult Finish(
        XmlSchemaSet set, ImportWalker walker, List<Diagnostic> diagnostics, string document)
    {
        try
        {
            set.Compile();
        }
        catch (XmlSchemaException failure)
        {
            diagnostics.Add(new Diagnostic(
                WsdlDiagnosticCodes.SchemaInvalid,
                DiagnosticSeverity.Error,
                "Le jeu de schémas de « " + Path.GetFileName(document) + " » ne compile pas : "
                    + failure.Message,
                Position: new Core.Syntax.SourcePosition(failure.LineNumber, failure.LinePosition)));
        }

        return new SchemaSetResult(
            XsdTypeReader.Read(set, document),
            XsdTypeReader.Elements(set),
            walker.Documents,
            walker.Unresolved);
    }
    /// <summary>
    /// Ajoute un schéma en ligne, avec l'URI de base du document qui le porte et ses préfixes.
    /// </summary>
    /// <remarks>
    /// Deux choses manquaient, et la seconde a coûté la première. Sans URI de base, un
    /// <c>schemaLocation</c> relatif écrit dans un schéma en ligne ne se résout contre rien, et le
    /// compilateur s'en plaint sur un contrat parfaitement sain. Mais la donner en sérialisant l'élément
    /// perdait les préfixes déclarés sur <c>wsdl:definitions</c> : un <c>type="tns:Charge"</c> est un nom
    /// qualifié <em>dans une valeur d'attribut</em>, que XLinq ne reconnaît pas comme tel et dont il
    /// n'emporte donc pas la déclaration. Le schéma devenait invalide, et la forme qui l'a montré — un
    /// type portant deux mille attributs — avait été écrite avant ce lecteur.
    /// <para>
    /// Les deux ensemble, donc : les déclarations des ancêtres sont recopiées sur une copie du schéma,
    /// celles qu'il porte déjà faisant foi.
    /// </para>
    /// </remarks>
    private static void Add(
        XmlSchemaSet set, InlineSchema schema, string document, List<Diagnostic> diagnostics)
    {
        using XmlReader reader = XmlReader.Create(
            new StringReader(WithInheritedPrefixes(schema.Element).ToString()),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null },
            new Uri(Path.GetFullPath(document)).ToString());

        try
        {
            _ = set.Add(schema.TargetNamespace, reader);
        }
        catch (XmlSchemaException failure)
        {
            diagnostics.Add(Invalid(failure, document));
        }
    }

    private static void Add(
        XmlSchemaSet set, string path, string document, List<Diagnostic> diagnostics)
    {
        try
        {
            _ = set.Add(null, path);
        }
        catch (XmlSchemaException failure)
        {
            diagnostics.Add(Invalid(failure, document));
        }
    }

    /// <summary>Rend une copie du schéma portant aussi les préfixes déclarés par ses ancêtres.</summary>
    private static XElement WithInheritedPrefixes(XElement schema)
    {
        XElement copy = new(schema);

        foreach (XAttribute declaration in Inherited(schema)
            .Where(declaration => copy.Attribute(declaration.Name) is null))
        {
            copy.Add(new XAttribute(declaration.Name, declaration.Value));
        }

        return copy;
    }

    private static IEnumerable<XAttribute> Inherited(XElement schema) =>
        schema.Ancestors()
            .SelectMany(ancestor => ancestor.Attributes())
            .Where(attribute => attribute.IsNamespaceDeclaration);

    private static Diagnostic Invalid(XmlSchemaException failure, string document) =>
        new(
            WsdlDiagnosticCodes.SchemaInvalid,
            DiagnosticSeverity.Error,
            "Un schéma de « " + Path.GetFileName(document) + " » est refusé : " + failure.Message,
            Position: new Core.Syntax.SourcePosition(failure.LineNumber, failure.LinePosition));

    private static readonly string[] Xsd11Constructs = ["assert", "alternative", "openContent"];

    /// <summary>
    /// Traduit une plainte du compilateur, en distinguant l'avertissement de l'erreur.
    /// </summary>
    /// <remarks>
    /// Le compilateur avertit, entre autres, qu'il n'a pas trouvé de schéma pour un espace de noms. Pris
    /// pour une erreur, cela ferait échouer tout contrat dont un import a été légitimement refusé par la
    /// politique — alors que ce refus est déjà consigné, avec sa raison.
    /// <para>
    /// Une plainte est en outre réécrite : celle qui porte sur « assert », « alternative » ou
    /// « openContent ». Le compilateur de .NET lit XSD 1.0 et dit « element is not supported in this
    /// context », ce qui n'apprend pas au développeur que son schéma est du XSD 1.1 ni que c'est là le
    /// désaccord. Le refus est le même ; ce qu'il enseigne ne l'est pas.
    /// </para>
    /// </remarks>
    private static Diagnostic Complaint(ValidationEventArgs argument, string document)
    {
        bool error = argument.Severity == XmlSeverityType.Error;

        if (error && Xsd11(argument.Message) is { } construct)
        {
            return new Diagnostic(
                WsdlDiagnosticCodes.Xsd11,
                DiagnosticSeverity.Error,
                "« " + Path.GetFileName(document) + " » emploie « xs:" + construct + " », une construction "
                    + "de XSD 1.1. L'outil lit XSD 1.0 : un type dont le sens dépend d'un « " + construct
                    + " » est déclaré non projetable plutôt que projeté sans lui, car le contrat produit "
                    + "serait alors plus permissif que le vrai.",
                Position: Where(argument));
        }

        return new Diagnostic(
            error ? WsdlDiagnosticCodes.SchemaInvalid : WsdlDiagnosticCodes.ImportUnresolved,
            error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
            "Le jeu de schémas de « " + Path.GetFileName(document) + " » : " + argument.Message,
            Position: Where(argument));
    }

    /// <summary>La construction XSD 1.1 dont une plainte parle, s'il y en a une.</summary>
    private static string? Xsd11(string message) =>
        Xsd11Constructs.FirstOrDefault(construct =>
            message.Contains(
                WsdlNamespaces.Schema + ":" + construct, StringComparison.Ordinal));

    private static Core.Syntax.SourcePosition? Where(ValidationEventArgs argument) =>
        argument.Exception is { } failure
            ? new Core.Syntax.SourcePosition(failure.LineNumber, failure.LinePosition)
            : null;
}
