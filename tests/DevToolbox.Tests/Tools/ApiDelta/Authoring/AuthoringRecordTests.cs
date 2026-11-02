using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Authoring;

/// <summary>
/// Ce que le registre des routes composées sait dire de lui-même.
/// </summary>
/// <remarks>
/// Les collisions sont rendues et non levées : une collision n'est pas une faute de programmation mais
/// une réponse du développeur qu'il faut lui rendre, avec ses deux côtés nommés pour qu'il sache entre
/// quoi et quoi choisir.
/// </remarks>
public sealed class AuthoringRecordTests
{
    [Fact]
    public void An_empty_record_is_a_value_and_not_a_null()
    {
        // Une étape qui ne peut pas rendre « rien » finit par rendre null, et le reste de la chaîne
        // apprend à tester null partout.
        AuthoringRecord.Empty.IsEmpty.Should().BeTrue();
        AuthoringRecord.Empty.Routes.Should().BeEmpty();
        AuthoringRecord.Empty.Dropped().Should().BeEmpty();
        AuthoringRecord.Empty.Placeholders().Should().BeEmpty();
        AuthoringRecord.Empty.Collisions(["GET /dossiers"]).Should().BeEmpty();
    }

    [Fact]
    public void A_route_colliding_with_production_names_both_sides()
    {
        AuthoringRecord record = new([Route("AjouterDocuments", "/Dossier/Ajouter")]);

        record.Collisions(["POST /Dossier/Ajouter"]).Should().ContainSingle()
            .Which.Should().Contain("AjouterDocuments").And.Contain("/Dossier/Ajouter")
            .And.Contain("production");
    }

    [Fact]
    public void Two_authored_routes_on_the_same_path_and_verb_name_both_operations()
    {
        AuthoringRecord record = new(
        [
            Route("AjouterDocuments", "/Dossier/Ajouter"),
            Route("AjouterIdentifiant", "/Dossier/Ajouter"),
        ]);

        record.Collisions([]).Should().ContainSingle()
            .Which.Should().Contain("AjouterDocuments").And.Contain("AjouterIdentifiant");
    }

    [Fact]
    public void Two_authored_routes_sharing_a_path_under_different_verbs_do_not_collide()
    {
        // Légal, et voulu : cela donne un élément de chemin portant deux opérations.
        AuthoringRecord record = new(
        [
            Route("ObtenirDossiers", "/Dossiers", HttpOperationMethod.Get),
            Route("CreerDossier", "/Dossiers", HttpOperationMethod.Post),
        ]);

        record.Collisions([]).Should().BeEmpty();
    }

    [Fact]
    public void Dropped_fields_are_reported_with_the_operation_they_came_from()
    {
        AuthoringRecord record = new(
        [
            Route("GetDocument", "/Document/{id}", HttpOperationMethod.Get,
            [
                new FieldPlacement("id", "string", false, ParameterLocation.Path, true),
                new FieldPlacement("groupes", "string", false, ParameterLocation.Nowhere, false),
            ]),
        ]);

        (string Operation, FieldPlacement Field) dropped =
            record.Dropped().Should().ContainSingle().Which;

        dropped.Operation.Should().Be("GetDocument");
        dropped.Field.Field.Should().Be("groupes");
    }

    [Fact]
    public void Every_authored_route_counts_as_a_description_still_to_write()
    {
        // Sans exception : aucune route composée ne porte de description que l'outil aurait rédigée.
        AuthoringRecord record = new(
        [
            Route("AjouterDocuments", "/Dossier/Ajouter"),
            Route("GetDocument", "/Document/{id}", HttpOperationMethod.Get),
        ]);

        record.Placeholders().Should().HaveCount(2);
    }

    [Fact]
    public void The_order_of_authoring_is_kept()
    {
        // Il est celui des points d'insertion : une route placée « après la précédente » désigne la
        // précédente de cette liste, et réordonner changerait où les routes atterrissent.
        AuthoringRecord record = new(
        [
            Route("Premiere", "/a"),
            Route("Deuxieme", "/b"),
            Route("Troisieme", "/c"),
        ]);

        record.Routes.Select(one => one.Operation)
            .Should().Equal("Premiere", "Deuxieme", "Troisieme");
    }

    private static AuthoredRoute Route(
        string operation,
        string path,
        HttpOperationMethod method = HttpOperationMethod.Post,
        IReadOnlyList<FieldPlacement>? placements = null) => new(
        operation,
        path,
        method,
        placements ?? [],
        [],
        InsertionPoint.AtEnd,
        RouteDerivationKind.Fallback,
        TookSuggestion: false);
}
