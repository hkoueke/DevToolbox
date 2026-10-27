using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Ce que les formes du contrat promettent d'elles-mêmes.
/// </summary>
/// <remarks>
/// Elles ne font presque rien, et ce presque est ce qui est éprouvé ici : les quelques membres calculés
/// portent des promesses qu'aucun compilateur ne tient. Qu'un contrat incomplet ne puisse pas se déclarer
/// complet, qu'une origine récupérée ne puisse pas porter un secret, qu'une politique incohérente se
/// reconnaisse.
/// </remarks>
public sealed class WsdlShapeTests
{
    private static readonly WsdlLocation Somewhere = new("catalogue.wsdl", "definitions", 1, 1);

    [Fact]
    public void A_contract_with_an_unresolved_import_cannot_claim_to_be_complete()
    {
        // La promesse la plus importante de ce type : calculée, jamais renseignée. Un contrat qui pourrait
        // s'affirmer complet alors qu'un import manque ferait rendre aux règles qui en dépendent un
        // résultat propre qu'elles n'ont pas établi.
        WsdlContract incomplete = Contract(
            [new UnresolvedImport("http://ailleurs", "ailleurs.xsd", Somewhere, "hors de la racine")]);

        incomplete.IsComplete.Should().BeFalse();
        Contract([]).IsComplete.Should().BeTrue();
    }

    [Fact]
    public void A_retrieved_origin_strips_the_credentials_and_the_query_string()
    {
        RetrievedOrigin origin = RetrievedOrigin.From(
            new Uri("https://utilisateur:motdepasse@partenaire.local/svc?token=abc123&wsdl"),
            DateTimeOffset.UnixEpoch);

        origin.RedactedUrl.Should().NotContain("motdepasse");
        origin.RedactedUrl.Should().NotContain("utilisateur");
        origin.RedactedUrl.Should().NotContain("abc123");
        origin.RedactedUrl.Should().Contain("partenaire.local");
    }

    [Fact]
    public void Every_redirect_of_a_retrieved_origin_is_redacted_too()
    {
        // Un seul saut non caviardé suffit à publier le jeton : la chaîne entière passe par la même porte.
        RetrievedOrigin origin = RetrievedOrigin.From(
            new Uri("https://partenaire.local/svc?wsdl"),
            DateTimeOffset.UnixEpoch,
            [
                new Uri("https://ancien.local/svc?token=secret1"),
                new Uri("https://relais.local/svc?token=secret2"),
            ]);

        origin.RedirectChain.Should().HaveCount(2);
        origin.RedirectChain.Should().OnlyContain(hop => !hop.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void A_policy_that_allows_the_remote_without_naming_a_host_is_not_coherent()
    {
        // Permettre le distant sans nommer d'hôte ne veut rien dire. Le lire comme « tous » serait la
        // lecture charitable, et la mauvaise.
        new ImportPolicy("C:\\racine", AllowRemote: true).IsCoherent.Should().BeFalse();

        new ImportPolicy("C:\\racine", AllowRemote: true, AllowedHosts: ["partenaire.local"])
            .IsCoherent.Should().BeTrue();

        ImportPolicy.ConfinedTo("C:\\racine").IsCoherent.Should().BeTrue("le fermé est cohérent");
    }

    [Fact]
    public void A_policy_confined_to_a_root_reaches_nothing_remote()
    {
        ImportPolicy policy = ImportPolicy.ConfinedTo("C:\\racine");

        policy.AllowRemote.Should().BeFalse();
        policy.AllowPlainHttp.Should().BeFalse();
        policy.AllowedHosts.Should().BeEmpty();
    }

    [Fact]
    public void An_operation_without_an_output_has_no_response_to_project()
    {
        WsdlOperation oneWay = Operation(MessagePattern.OneWay, output: null);

        oneWay.HasResponse.Should().BeFalse();
        Operation(MessagePattern.RequestResponse, Message("sortie")).HasResponse.Should().BeTrue();
    }

    [Fact]
    public void An_operation_no_binding_exposes_is_unreachable()
    {
        // Déclarée dans un type de port et liée par rien : aucun appelant ne peut l'atteindre. Ce n'est
        // pas un détail de style, c'est une erreur de contrat.
        Operation(MessagePattern.RequestResponse, Message("sortie")).IsUnreachable.Should().BeTrue();

        (Operation(MessagePattern.RequestResponse, Message("sortie")) with { Binding = Binding() })
            .IsUnreachable.Should().BeFalse();
    }

    [Fact]
    public void A_part_naming_both_an_element_and_a_type_is_not_well_formed()
    {
        new WsdlPart("corps", "tns:element", null, Somewhere).IsWellFormed.Should().BeTrue();
        new WsdlPart("corps", null, "tns:type", Somewhere).IsWellFormed.Should().BeTrue();
        new WsdlPart("corps", "tns:element", "tns:type", Somewhere).IsWellFormed.Should().BeFalse();
        new WsdlPart("corps", null, null, Somewhere).IsWellFormed.Should().BeFalse();
    }

    [Fact]
    public void An_encoded_or_rpc_binding_says_its_shape_is_not_determined_by_the_schema()
    {
        (Binding() with { Use = "encoded" }).ShapeIsNotDeterminedBySchema.Should().BeTrue();
        (Binding() with { Style = "rpc" }).ShapeIsNotDeterminedBySchema.Should().BeTrue();
        Binding().ShapeIsNotDeterminedBySchema.Should().BeFalse("document et literal se déduisent");
    }

    [Fact]
    public void A_type_carrying_attributes_admits_it_cannot_be_carried_whole()
    {
        XsdType plain = Type([], XsdContentKind.Sequence);

        plain.HasUncarryableParts.Should().BeFalse();

        Type([new XsdAttributeUse("version", "xs:int", false, Somewhere)], XsdContentKind.Sequence)
            .HasUncarryableParts.Should().BeTrue("un attribut n'a aucun équivalent");

        Type([], XsdContentKind.Mixed)
            .HasUncarryableParts.Should().BeTrue("un contenu mixte non plus");

        (plain with { Derivation = XsdDerivation.Restriction })
            .HasUncarryableParts.Should().BeTrue("ni une dérivation par restriction");
    }

    [Fact]
    public void A_particle_reports_its_cardinality_rather_than_leaving_it_to_be_deduced()
    {
        // minOccurs vaut un par défaut en XSD : un élément ajouté à une séquence est obligatoire par
        // construction, et c'est le piège de versionnage le plus courant de SOAP.
        new XsdParticle("titre", "xs:string", 1, 1, false, Somewhere).IsRequired.Should().BeTrue();
        new XsdParticle("note", "xs:string", 0, 1, false, Somewhere).IsRequired.Should().BeFalse();
        new XsdParticle("mots", "xs:string", 0, null, false, Somewhere).IsCollection.Should().BeTrue();
        new XsdParticle("titre", "xs:string", 1, 1, false, Somewhere).IsCollection.Should().BeFalse();
    }

    [Fact]
    public void A_location_names_its_document_because_a_contract_is_a_graph_of_files()
    {
        string display = new WsdlLocation(
            "C:\\contrats\\catalogue-types.xsd", "schema/complexType[Document]", 42, 7).Display;

        display.Should().Contain("catalogue-types.xsd");
        display.Should().Contain("complexType[Document]");
        display.Should().Contain("42");
    }

    private static WsdlContract Contract(IReadOnlyList<UnresolvedImport> unresolved) => new(
        [new WsdlDocument("catalogue.wsdl", WsdlDocumentKind.Definitions, 10, "abc")],
        [],
        new Dictionary<string, XsdType>(StringComparer.Ordinal),
        [],
        unresolved,
        "C:\\contrats");

    private static WsdlOperation Operation(MessagePattern pattern, WsdlMessage? output) => new(
        "lireDocument", pattern, Message("entree"), output, [], null, null, Somewhere);

    private static WsdlMessage Message(string name) => new(name, [], Somewhere);

    private static WsdlBinding Binding() => new(
        "CatalogueBinding", "document", "literal", "http", SoapVersion.Soap11, "action", Somewhere);

    private static XsdType Type(IReadOnlyList<XsdAttributeUse> attributes, XsdContentKind content) => new(
        "tns:Document", content, null, XsdDerivation.None, [], attributes, [], false, false, [], [],
        Somewhere);
}
