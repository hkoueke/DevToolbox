using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Une route composée atterrit-elle au chemin, au verbe et à l'endroit que le développeur a choisis ?
/// </summary>
/// <remarks>
/// L'épreuve de la tranche. La dérivation d'un itinéraire depuis un nom d'opération a été mesurée sur une
/// paire réelle : dix-neuf routes produites, aucune ne tombant sur son équivalent. Ce que ces essais
/// établissent est que la réponse du développeur passe désormais devant la devinette de l'outil, jusqu'aux
/// octets écrits.
/// </remarks>
public sealed class AuthoredRouteEndToEndTests
{
    [Fact]
    public void An_authored_path_and_verb_replace_the_derived_ones()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        OperationSelector derived = run.Route("dossierModifie");

        // Un chemin qu'aucune règle de nommage ne produirait, et un verbe que la dérivation n'a pas
        // choisi : c'est ce que le développeur tape quand sa maison écrit du RPC.
        AuthoringRecord authored = new(
        [
            Authored("dossierModifie", "/Dossier/NotifierModification", HttpOperationMethod.Post),
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([derived]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        PointerNavigator.Find(corrected.Root, "/paths/~1Dossier~1NotifierModification/post")
            .Should().NotBeNull("la route doit être au chemin composé");

        PointerNavigator.Find(corrected.Root, Pointer(derived))
            .Should().BeNull("et nulle part à l'itinéraire dérivé");
    }

    [Fact]
    public void Without_an_authoring_record_the_derived_route_is_still_used()
    {
        // Le chemin d'avant, inchangé : un compagnon OpenAPI ne compose rien, et une route reprise garde
        // l'itinéraire qu'elle porte.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        OperationSelector derived = run.Route("dossierModifie");

        Result<BuiltCorrection> built = run.Build(new CorrectionSelection([derived]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        PointerNavigator.Find(run.Reread(built.Value.Serialized).Root, Pointer(derived))
            .Should().NotBeNull();
    }

    [Fact]
    public void An_authored_route_lands_where_it_was_asked_to_land()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        IReadOnlyList<string> before = Paths(run.Production);

        before.Should().HaveCountGreaterThan(1, "il faut un voisin à nommer");

        string neighbour = before[0];

        AuthoringRecord authored = new(
        [
            Authored("dossierModifie", "/Dossier/NotifierModification", HttpOperationMethod.Post)
                with { Insertion = InsertionPoint.After(neighbour) },
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("dossierModifie")]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        IReadOnlyList<string> after = Paths(run.Reread(built.Value.Serialized));

        after[1].Should().Be(
            "/Dossier/NotifierModification", "la route doit suivre le voisin nommé");

        // Et l'ordre de ce que la production portait est intact : la nouvelle s'insère, elle ne
        // réordonne rien. C'est l'assertion qui attrape une régression de l'épisseur.
        after.Where(path => before.Contains(path, StringComparer.Ordinal))
            .Should().Equal(before, "les chemins existants gardent leur ordre");
    }

    [Fact]
    public void Everything_production_said_is_still_there_after_authoring()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        AuthoringRecord authored = new(
        [
            Authored("dossierModifie", "/Dossier/NotifierModification", HttpOperationMethod.Post),
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("dossierModifie")]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string after = Encoding.UTF8.GetString(built.Value.Serialized.Span);
        string production = Encoding.UTF8.GetString(run.Production.Source.Span);

        foreach (string line in production.Split('\n').Where(one => one.Trim().Length > 0))
        {
            after.Should().Contain(
                line.TrimEnd('\r'), "« " + line.Trim() + " » venait de la production");
        }
    }

    [Fact]
    public void The_verification_still_establishes_that_nothing_else_moved()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        AuthoringRecord authored = new(
        [
            Authored("dossierModifie", "/Dossier/NotifierModification", HttpOperationMethod.Post),
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("dossierModifie")]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        CorrectionVerification verification = run.Verify(built.Value);

        verification.Reparsed.Should().BeTrue();
        verification.Revalidated.Should().BeTrue(string.Join(" · ", verification.Complaints));
        verification.ReferencesResolved.Should().BeTrue(string.Join(" · ", verification.Complaints));
    }

    [Fact]
    public void A_get_with_placed_fields_declares_parameters_and_no_body()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Delivered("placements.wsdl", "placements.wsdl");

        // Le cas de la spécification : un champ dans le chemin, deux dans la requête, un écarté.
        AuthoringRecord authored = new(
        [
            Authored("creerClient", "/Document/{id}", HttpOperationMethod.Get) with
            {
                Placements =
                [
                    Scalar("id", ParameterLocation.Path),
                    Scalar("enabled", ParameterLocation.Query, required: true),
                    Scalar("label", ParameterLocation.Query),
                    Scalar("ignored", ParameterLocation.Nowhere),
                ],
            },
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("creerClient")]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = run.Reread(built.Value.Serialized);
        const string Operation = "/paths/~1Document~1{id}/get";

        SequenceNode listed = PointerNavigator.Find(corrected.Root, Operation + "/parameters")
            .Should().BeOfType<SequenceNode>().Subject;

        listed.Items.Should().HaveCount(3, "trois champs placés, le quatrième écarté");

        Where(listed, "id").Should().Be("path");
        Where(listed, "enabled").Should().Be("query");
        Where(listed, "label").Should().Be("query");

        Required(listed, "id").Should().Be("true", "un paramètre de chemin l'est toujours");
        Required(listed, "enabled").Should().Be("true", "le développeur l'a dit");
        Required(listed, "label").Should().Be("false", "le contrat ne dit rien, et il n'a rien dit");

        PointerNavigator.Find(corrected.Root, Operation + "/requestBody")
            .Should().BeNull("aucun champ ne va dans le corps");

        listed.Items.Cast<MappingNode>().Select(parameter => (parameter.Find("name") as ScalarNode)?.Text)
            .Should().NotContain("ignored", "le champ écarté ne devient pas un paramètre");
    }

    [Fact]
    public void A_write_verb_keeps_the_body_and_its_derived_reference()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        // « archiverDocument » porte une entrée et la production ne la couvre pas, là où
        // « dossierModifie » est une notification sans
        // entrée — ce que la première version de cet essai a appris en échouant sur elle.
        AuthoringRecord authored = new(
        [
            Authored("archiverDocument", "/Document/Archiver", HttpOperationMethod.Post),
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("archiverDocument")]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        PointerNavigator.Find(corrected.Root, "/paths/~1Document~1Archiver/post/requestBody")
            .Should().NotBeNull("le corps est repris, avec la référence que la projection a dérivée");
    }

    [Fact]
    public void An_operation_whose_every_field_goes_to_the_body_is_passed_through_unchanged()
    {
        // La règle que l'essai précédent a révélée en échouant : quand rien n'est redistribué, l'opération
        // projetée passe telle quelle. Réassembler pour rien changerait des octets que personne n'a
        // demandé de changer — et une notification sans entrée n'a pas de corps à porter.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        AuthoringRecord authored = new(
        [
            Authored("dossierModifie", "/Dossier/Notifier", HttpOperationMethod.Post),
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("dossierModifie")]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        PointerNavigator.Find(corrected.Root, "/paths/~1Dossier~1Notifier/post")
            .Should().NotBeNull("la route est portée, au chemin composé");

        PointerNavigator.Find(corrected.Root, "/paths/~1Dossier~1Notifier/post/requestBody")
            .Should().BeNull("une notification sans entrée n'a pas de corps, et rien n'en invente un");
    }

    [Fact]
    public void A_dropped_field_is_named_in_the_record_and_absent_from_the_document()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Delivered("placements.wsdl", "placements.wsdl");

        AuthoringRecord authored = new(
        [
            Authored("creerClient", "/Document/Chercher", HttpOperationMethod.Get) with
            {
                Placements =
                [
                    Scalar("id", ParameterLocation.Query),
                    Scalar("ignored", ParameterLocation.Nowhere),
                ],
            },
        ]);

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("creerClient")]), authoring: authored);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        // Consigné : un champ ne disparaît pas sans être nommé quelque part.
        (string Operation, FieldPlacement Field) dropped =
            authored.Dropped().Should().ContainSingle().Which;

        dropped.Operation.Should().Be("creerClient");
        dropped.Field.Field.Should().Be("ignored");

        SpecNode corrected = run.Reread(built.Value.Serialized).Root;
        At<SequenceNode>(corrected, "/paths/~1Document~1Chercher/get/parameters").Items.Cast<MappingNode>()
            .Select(parameter => (parameter.Find("name") as ScalarNode)?.Text).Should().NotContain("ignored");
    }

    [Fact]
    public void Mixed_placements_survive_emission_with_restricted_scalar_schemas_and_filtered_body()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Delivered("placements.wsdl", "placements.wsdl");
        var fields = InputFields.Of(run.Contract, run.Contract.Operations.Single(operation => string.Equals(operation.Name, "creerClient", StringComparison.Ordinal)));
        fields.Single(field => string.Equals(field.Field, "code", StringComparison.Ordinal)).IsComposite.Should().BeFalse();
        fields.Single(field => string.Equals(field.Field, "child", StringComparison.Ordinal)).IsComposite.Should().BeTrue();
        fields.Single(field => string.Equals(field.Field, "tags", StringComparison.Ordinal)).IsComposite.Should().BeTrue();
        FieldPlacement[] placements = [.. fields.Select(field => new FieldPlacement(field.Field, field.Declared,
            field.IsComposite, field.Field switch
            {
                "id" => ParameterLocation.Path,
                "enabled" => ParameterLocation.Query,
                "code" => ParameterLocation.Header,
                "ignored" => ParameterLocation.Nowhere,
                _ => ParameterLocation.Body,
            }, string.Equals(field.Field, "label", StringComparison.Ordinal), string.Equals(field.Field, "code", StringComparison.Ordinal) ? "X-Code" : null))];
        AuthoringRecord authored = new([Authored("creerClient", "/Clients/{id}", HttpOperationMethod.Post)
            with { Placements = placements }]);
        var built = run.Build(new CorrectionSelection([run.Route("creerClient")]), authoring: authored);
        built.IsSuccess.Should().BeTrue(built.Failure?.Message);
        SpecNode root = run.Reread(built.Value.Serialized).Root;
        const string Operation = "/paths/~1Clients~1{id}/post";
        At<MappingNode>(root, Operation + "/requestBody/content/application~1json/schema/properties")
            .Entries.Select(entry => entry.Key.Text).Should().Equal("label", "child", "tags");
        At<SequenceNode>(root, Operation + "/requestBody/content/application~1json/schema/required")
            .Items.Cast<ScalarNode>().Select(node => node.Text).Should().Equal("label");
        At<ScalarNode>(root, Operation + "/parameters/1/schema/type").Text.Should().Be("boolean");
        At<ScalarNode>(root, Operation + "/parameters/2/name").Text.Should().Be("X-Code");
        string identifier = At<ScalarNode>(root, Operation + "/parameters/0/schema/$ref").Text[1..];
        At<ScalarNode>(root, identifier + "/type").Text.Should().Be("integer");
        At<ScalarNode>(root, identifier + "/minimum").Text.Should().Be("1");
        At<ScalarNode>(root, identifier + "/maximum").Text.Should().Be("99");
        string code = At<ScalarNode>(root, Operation + "/parameters/2/schema/$ref").Text[1..];
        At<SequenceNode>(root, code + "/enum").Items.Cast<ScalarNode>().Select(node => node.Text).Should().Equal("AA", "BB");
        At<ScalarNode>(root, code + "/minLength").Text.Should().Be("2");
        At<ScalarNode>(root, code + "/maxLength").Text.Should().Be("4");
        var verification = run.Verify(built.Value);
        verification.Reparsed.Should().BeTrue();
        verification.Revalidated.Should().BeTrue(string.Join(" · ", verification.Complaints));
        verification.ReferencesResolved.Should().BeTrue(string.Join(" · ", verification.Complaints));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Multiple_new_paths_can_share_an_original_anchor_without_reordering_production(bool atEnd)
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Delivered("placements.wsdl", "placements.wsdl");
        IReadOnlyList<string> before = Paths(run.Production);
        InsertionPoint insertion = atEnd ? InsertionPoint.AtEnd : InsertionPoint.After(before[0]);
        AuthoringRecord authored = new([
            Authored("creerClient", "/First", HttpOperationMethod.Post) with { Insertion = insertion },
            Authored("modifierClient", "/Second", HttpOperationMethod.Post) with { Insertion = insertion },
        ]);
        var built = run.Build(new CorrectionSelection([run.Route("creerClient"), run.Route("modifierClient")]), authoring: authored);
        built.IsSuccess.Should().BeTrue(built.Failure?.Message);
        IReadOnlyList<string> after = Paths(run.Reread(built.Value.Serialized));
        after.Where(path => before.Contains(path, StringComparer.Ordinal)).Should().Equal(before);
        after.Skip(atEnd ? before.Count : 1).Take(2).Should().Equal("/First", "/Second");
        var verification = run.Verify(built.Value);
        verification.Reparsed.Should().BeTrue();
        verification.Revalidated.Should().BeTrue(string.Join(" · ", verification.Complaints));
        verification.ReferencesResolved.Should().BeTrue(string.Join(" · ", verification.Complaints));
    }

    private static T At<T>(SpecNode root, string pointer) where T : SpecNode =>
        PointerNavigator.Find(root, pointer).Should().BeOfType<T>().Subject;

    private static string Where(SequenceNode listed, string name) => Member(listed, name, "in");

    private static string Required(SequenceNode listed, string name) =>
        Member(listed, name, "required");

    private static string Member(SequenceNode listed, string name, string member)
    {
        MappingNode parameter = listed.Items
            .OfType<MappingNode>()
            .Single(one => one.Find("name") is ScalarNode named
                && string.Equals(named.Text, name, StringComparison.Ordinal));

        return parameter.Find(member).Should().BeOfType<ScalarNode>().Subject.Text;
    }

    private static FieldPlacement Scalar(
        string field, ParameterLocation location, bool required = false) =>
        new(field, "string", IsComposite: false, location, required);

    private static string Pointer(OperationSelector route) =>
        SpecPointer.Root.Child("paths").Child(route.Path).Child(route.MethodKey).Json;

    private static IReadOnlyList<string> Paths(SpecDocument document) =>
        PointerNavigator.Find(document.Root, "/paths") is MappingNode paths
            ? [.. paths.Entries.Select(entry => entry.Key.Text)]
            : [];

    private static AuthoredRoute Authored(
        string operation, string path, HttpOperationMethod method) => new(
        operation,
        path,
        method,
        [],
        [],
        InsertionPoint.AtEnd,
        RouteDerivationKind.Fallback,
        TookSuggestion: false);
}
