using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Le lecteur lit-il le graphe entier, et refuse-t-il en nommant ce qu'il n'a pas pu lire ?
/// </summary>
/// <remarks>
/// Le refus compte autant que la lecture, et davantage. Un contrat dont les types vivent dans un fichier
/// qui n'a pas été lu est un contrat dont l'outil ne sait rien — et un rapport assuré bâti là-dessus est
/// indiscernable d'un bon rapport, ce qui en fait le pire des deux.
/// </remarks>
public sealed class WsdlReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-lecture-wsdl-" + Guid.NewGuid().ToString("N"));

    private readonly WsdlReader _reader = new();

    public WsdlReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task The_ordinary_graph_reads_as_three_documents_with_their_hashes()
    {
        string entry = Materialise("catalogue.wsdl", "catalogue-types.xsd", "commun.xsd");

        WsdlReadResult result = await ReadAsync(entry);

        result.IsFaithful.Should().BeTrue(Said(result));

        WsdlContract contract = result.Contract!;

        contract.Documents.Should().HaveCount(3, "le wsdl, ses types, et le commun qu'ils importent");
        contract.Documents.Should().OnlyContain(document => document.ContentHash.Length == 64);
        contract.IsComplete.Should().BeTrue("aucun import n'a été refusé");
        contract.ConfinementRoot.Should().Be(_root, "le périmètre que l'outil s'est donné est dit");
    }

    [Fact]
    public async Task The_four_message_patterns_are_read_as_written()
    {
        WsdlContract contract = await ReadOrdinaryAsync();

        Pattern(contract, "deposerDocument").Should().Be(MessagePattern.RequestResponse);
        Pattern(contract, "archiverDocument").Should().Be(MessagePattern.OneWay);
        Pattern(contract, "dossierModifie").Should().Be(MessagePattern.Notification);
        Pattern(contract, "interrogerDossier").Should().Be(MessagePattern.SolicitResponse);
    }

    [Fact]
    public async Task An_operation_with_no_output_has_no_response_to_project()
    {
        WsdlContract contract = await ReadOrdinaryAsync();

        Operation(contract, "archiverDocument").HasResponse.Should().BeFalse();
        Operation(contract, "deposerDocument").HasResponse.Should().BeTrue();
    }

    [Fact]
    public async Task Every_operation_is_bound_and_carries_its_style_and_action()
    {
        WsdlContract contract = await ReadOrdinaryAsync();

        contract.Unreachable.Should().BeEmpty("le catalogue lie chacune de ses opérations");

        WsdlBinding binding = Operation(contract, "lireDocument").Binding!;

        binding.Style.Should().Be("document");
        binding.Use.Should().Be("literal");
        binding.Soap.Should().Be(SoapVersion.Soap11);
        binding.SoapAction.Should().Contain("lireDocument");
        binding.ShapeIsNotDeterminedBySchema.Should().BeFalse();
    }

    [Fact]
    public async Task The_compiled_types_carry_what_the_projection_will_need_to_read()
    {
        WsdlContract contract = await ReadOrdinaryAsync();

        XsdType document = Type(contract, "Document");

        document.Content.Should().Be(XsdContentKind.Sequence);
        document.Attributes.Should().HaveCount(2, "version et verrouille n'ont aucun équivalent");
        document.HasUncarryableParts.Should().BeTrue("et le type l'admet de lui-même");

        Particle(document, "priorite").IsRequired.Should().BeFalse();
        Particle(document, "titre").IsRequired.Should().BeTrue();
        Particle(document, "motsCles").IsCollection.Should().BeTrue();
        Particle(document, "resume").Nillable.Should().BeTrue();

        Type(contract, "Critere").Content.Should().Be(XsdContentKind.Choice);
        Type(contract, "Pagination").Content.Should().Be(XsdContentKind.All);
        Type(contract, "DocumentDetaille").Derivation.Should().Be(XsdDerivation.Extension);
        Type(contract, "Identifiant").Facets.Select(facet => facet.Kind)
            .Should().Contain("maxLength").And.Contain("pattern");
        Type(contract, "Etat").Facets.Should().HaveCount(3, "les trois valeurs de l'énumération");
    }

    [Fact]
    public async Task A_missing_schema_makes_the_run_refuse_and_names_what_wanted_it()
    {
        // Le commun est délibérément absent. L'outil doit nommer l'emplacement manquant, le document qui
        // le voulait, et sa position — sans quoi le développeur sait qu'il manque quelque chose sans
        // savoir où aller le chercher.
        string entry = Materialise("catalogue.wsdl", "catalogue-types.xsd");

        WsdlReadResult result = await ReadAsync(entry);

        result.IsFaithful.Should().BeFalse("un contrat à moitié lu n'est pas un contrat");

        Diagnostic refusal = result.Diagnostics
            .Should().ContainSingle(diagnostic =>
                diagnostic.Code == WsdlDiagnosticCodes.ImportUnresolved && diagnostic.IsBlocking)
            .Subject;

        refusal.Message.Should().Contain("commun.xsd", "l'emplacement manquant est nommé");
        refusal.Message.Should().Contain("catalogue-types.xsd", "et le document qui le voulait aussi");
        refusal.Position!.Line.Should().BeGreaterThan(0, "avec sa position");
    }

    [Fact]
    public void A_document_type_definition_is_refused_before_anything_is_resolved()
    {
        WsdlReadResult result = ReadBytes("hostile.doctype.wsdl");

        result.IsFaithful.Should().BeFalse();

        Diagnostic refusal = result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.DocumentTypeDefinition)
            .Subject;

        refusal.Message.Should().Contain("définition de type de document");
        refusal.Message.Should().Contain("sous aucune configuration");
    }

    [Fact]
    public void A_cascade_of_entities_is_closed_by_the_same_refusal()
    {
        // Pas de budget d'expansion à régler, pas d'heuristique : interdire les définitions de type de
        // document ferme XXE et l'expansion en cascade du même geste.
        WsdlReadResult result = ReadBytes("hostile.billion-laughs.wsdl");

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should()
            .Contain(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.DocumentTypeDefinition);
    }

    [Fact]
    public async Task An_import_leaving_the_confinement_root_is_refused_by_naming_the_path()
    {
        string entry = Materialise("hostile.traversal.wsdl");

        WsdlReadResult result = await ReadAsync(entry);

        result.IsFaithful.Should().BeFalse();

        Diagnostic refusal = result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.ImportOutsideRoot)
            .Subject;

        refusal.Message.Should().Contain("quitte la racine");
        refusal.Message.Should().Contain(_root, "la racine est nommée, pas seulement invoquée");
    }

    [Fact]
    public async Task A_legal_cycle_is_recorded_once_and_never_followed_twice()
    {
        string entry = Materialise("hostile.cycle.wsdl", "hostile.cycle-a.xsd", "hostile.cycle-b.xsd");

        WsdlReadResult result = await ReadAsync(entry);

        result.IsFaithful.Should().BeTrue(Said(result));
        result.Contract!.Documents.Should().HaveCount(3, "le wsdl et les deux schémas, une fois chacun");

        result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.ImportCycle)
            .Which.Severity.Should().Be(
                DiagnosticSeverity.Information, "un cycle est légal : consigné, pas reproché");
    }

    [Fact]
    public void A_wsdl_two_document_is_refused_by_naming_the_versions_supported()
    {
        const string Wsdl20 = """
            <?xml version="1.0" encoding="utf-8"?>
            <description xmlns="http://www.w3.org/ns/wsdl" targetNamespace="urn:x" />
            """;

        WsdlReadResult result = _reader.ReadBytes(
            Encoding.UTF8.GetBytes(Wsdl20), "futur.wsdl", WsdlLimits.Default, Policy());

        result.IsFaithful.Should().BeFalse();

        result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.UnsupportedVersion)
            .Which.Message.Should().Contain("WSDL 1.1");
    }

    [Fact]
    public void A_document_over_the_size_budget_is_refused_with_the_limit_named()
    {
        WsdlReadResult result = _reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read("Wsdl.catalogue.wsdl")),
            "catalogue.wsdl",
            WsdlLimits.Default with { MaxDocumentBytes = 64 },
            Policy());

        result.IsFaithful.Should().BeFalse();

        result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached)
            .Which.Message.Should().Contain("64", "la limite est nommée, pas seulement atteinte");
    }

    [Fact]
    public async Task A_redefine_is_refused_rather_than_read_and_ignored()
    {
        // Ignorer un redefine change le sens du document sans le dire. Le refuser est le seul choix
        // honnête tant qu'il n'est pas appliqué.
        await File.WriteAllTextAsync(
            Path.Combine(_root, "redefini.xsd"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:a">
              <xs:redefine schemaLocation="commun.xsd" />
            </xs:schema>
            """);

        string entry = Write("entree.wsdl", """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:a">
              <wsdl:types>
                <xs:schema targetNamespace="urn:a">
                  <xs:redefine schemaLocation="redefini.xsd" />
                </xs:schema>
              </wsdl:types>
            </wsdl:definitions>
            """);

        WsdlReadResult result = await ReadAsync(entry);

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should()
            .Contain(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.Redefine);
    }

    [Fact]
    public async Task A_remote_import_is_refused_while_the_policy_keeps_it_closed()
    {
        string entry = Write("distant.wsdl", """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:a">
              <wsdl:types>
                <xs:schema targetNamespace="urn:a">
                  <xs:import namespace="urn:b" schemaLocation="https://ailleurs.local/types.xsd" />
                </xs:schema>
              </wsdl:types>
            </wsdl:definitions>
            """);

        WsdlReadResult result = await ReadAsync(entry);

        result.IsFaithful.Should().BeFalse();

        // Refusé par la politique, et non par une expiration : le message le dit, et l'essai est rapide.
        result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.ImportRemoteRefused)
            .Which.Message.Should().Contain("fermée");
    }

    [Fact]
    public void A_policy_allowing_the_remote_without_a_host_refuses_the_run_before_reading()
    {
        WsdlReadResult result = _reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read("Wsdl.catalogue.wsdl")),
            "catalogue.wsdl",
            WsdlLimits.Default,
            new ImportPolicy(_root, AllowRemote: true));

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.ImportRemoteRefused)
            .Which.Message.Should().Contain("n'autorise pas tout");
    }

    [Fact]
    public async Task An_import_chain_past_the_budget_exhausts_a_named_limit_rather_than_a_stack()
    {
        // La leçon de la tranche précédente, appliquée d'avance : une chaîne plausible y avait fait
        // déborder la pile, sans diagnostic et sans position.
        string entry = GeneratedWsdl.Chain(Path.Combine(_root, "chaine"), 120);

        WsdlReadResult result = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default with { MaxImportChainDepth = 20 },
            ImportPolicy.ConfinedTo(Path.Combine(_root, "chaine")),
            CancellationToken.None);

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should()
            .Contain(diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached)
            .And.Contain(diagnostic => diagnostic.Message.Contains("20", StringComparison.Ordinal));
    }


    [Fact]
    public async Task An_ordinary_type_derives_from_nothing_rather_than_from_anyType()
    {
        // Quatre défauts que les seize premiers essais laissaient passer, et que seul un vidage de ce que
        // le lecteur produit vraiment a montrés. Celui-ci était le pire : « DerivedBy » rend Restriction
        // pour un type complexe ordinaire, si bien que chaque type du contrat se déclarait non portable.
        WsdlContract contract = await ReadOrdinaryAsync();

        Type(contract, "Dossier").Derivation.Should().Be(XsdDerivation.None);
        Type(contract, "Critere").Derivation.Should().Be(XsdDerivation.None);
        Type(contract, "Erreur").Derivation.Should().Be(XsdDerivation.None);

        Type(contract, "Dossier").HasUncarryableParts.Should()
            .BeFalse("une séquence d'éléments nommés se porte entièrement");
    }

    [Fact]
    public async Task The_builtin_types_of_xsd_are_not_part_of_the_contract()
    {
        WsdlContract contract = await ReadOrdinaryAsync();

        contract.Types.Keys.Should().NotContain(
            key => key.Contains("XMLSchema", StringComparison.Ordinal),
            "« anyType » y figurait, et la projection l'aurait parcouru comme un type du service");
    }

    [Fact]
    public async Task A_substitution_member_belongs_to_the_type_of_its_head()
    {
        // Le type de la tête, et non celui du membre. L'erreur donnait un compte plausible — un de chaque
        // côté — pour une attribution fausse, ce qu'aucune assertion sur un nombre n'aurait montré.
        WsdlContract contract = await ReadOrdinaryAsync();

        Type(contract, "Document").SubstitutionMembers.Should().HaveCount(
            2, "les deux membres ont « piece », de type Document, pour tête");

        Type(contract, "DocumentDetaille").SubstitutionMembers.Should().BeEmpty(
            "aucune tête n'est de ce type, bien qu'un membre le soit");
    }

    [Fact]
    public async Task A_sound_contract_produces_no_diagnostic_at_all()
    {
        // Un avertissement parasite reste un avertissement : il apprend au développeur que l'outil se
        // plaint sans raison, et le jour où la plainte compte il ne la lira plus.
        WsdlReadResult result = await ReadAsync(
            Materialise("catalogue.wsdl", "catalogue-types.xsd", "commun.xsd"));

        result.Diagnostics.Should().BeEmpty(Said(result));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<WsdlContract> ReadOrdinaryAsync()
    {
        WsdlReadResult result = await ReadAsync(
            Materialise("catalogue.wsdl", "catalogue-types.xsd", "commun.xsd"));

        result.IsFaithful.Should().BeTrue(Said(result));

        return result.Contract!;
    }

    private Task<WsdlReadResult> ReadAsync(string entry) =>
        _reader.ReadAsync(entry, WsdlLimits.Default, Policy(), CancellationToken.None);

    private WsdlReadResult ReadBytes(string name) =>
        _reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read("Wsdl." + name)),
            name,
            WsdlLimits.Default,
            Policy());

    private ImportPolicy Policy() => ImportPolicy.ConfinedTo(_root);

    /// <summary>Écrit des documents du corpus sur le disque, et rend le premier.</summary>
    private string Materialise(params string[] names)
    {
        string? first = null;

        foreach (string name in names)
        {
            string bare = name[(name.LastIndexOf('.', name.Length - 5) + 1)..];

            first ??= Write(bare, CorpusLoader.Read("Wsdl." + name));
        }

        foreach (string name in names.Skip(1))
        {
            string bare = name[(name.LastIndexOf('.', name.Length - 5) + 1)..];

            _ = Write(bare, CorpusLoader.Read("Wsdl." + name));
        }

        return first!;
    }

    private string Write(string name, string content)
    {
        string path = Path.Combine(_root, name);

        File.WriteAllText(path, content, new UTF8Encoding(false));

        return path;
    }

    private static string Said(WsdlReadResult result) =>
        string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message));

    private static WsdlOperation Operation(WsdlContract contract, string name) =>
        contract.Operations.Single(operation =>
            string.Equals(operation.Name, name, StringComparison.Ordinal));

    private static MessagePattern Pattern(WsdlContract contract, string name) =>
        Operation(contract, name).Pattern;

    private static XsdType Type(WsdlContract contract, string local) =>
        contract.Types.Single(pair =>
            pair.Key.EndsWith(":" + local, StringComparison.Ordinal)
            || string.Equals(pair.Key, local, StringComparison.Ordinal)).Value;

    private static XsdParticle Particle(XsdType type, string name) =>
        type.Particles.Single(particle =>
            string.Equals(particle.Name, name, StringComparison.Ordinal));
}
