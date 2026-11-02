using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Authoring;

/// <summary>
/// Ce qu'un placement ne peut pas être, et ce qu'une route composée doit tenir dans son ensemble.
/// </summary>
/// <remarks>
/// Chaque refus est éprouvé sur ce qu'il nomme, et non seulement sur le fait qu'il refuse. Un message qui
/// dirait « placement invalide » obligerait le développeur à chercher lequel parmi vingt champs, et c'est
/// le genre de message qu'on apprend à ignorer.
/// </remarks>
public sealed class PlacementRulesTests
{
    [Theory]
    [InlineData(ParameterLocation.Path)]
    [InlineData(ParameterLocation.Query)]
    [InlineData(ParameterLocation.Header)]
    public void A_composite_is_refused_where_openapi_expects_a_scalar(ParameterLocation location)
    {
        PlacementRules.Allows(composite: true, location).Should().BeFalse();

        // L'invite a besoin d'une phrase avant de construire l'objet : remonter une exception à travers
        // une conversation serait demander pardon plutôt que permission.
        FieldPlacement inBody = Composite(ParameterLocation.Body);

        string? refusal = PlacementRules.Check(inBody with { Location = ParameterLocation.Body });

        refusal.Should().BeNull("le corps accepte un composite");
    }

    [Theory]
    [InlineData(ParameterLocation.Path, "chemin")]
    [InlineData(ParameterLocation.Query, "requête")]
    [InlineData(ParameterLocation.Header, "en-tête")]
    public void The_refusal_names_the_field_its_type_and_the_destination(
        ParameterLocation location, string where)
    {
        // Construire le placement lève, puisque le type tient son propre invariant. C'est la raison
        // d'être de « Allows » : l'invite demande d'abord, et ne construit qu'ensuite.
        PlacementRules.Allows(composite: true, location).Should().BeFalse();

        FieldPlacement refused = Composite(ParameterLocation.Body) with { Field = "request" };

        string? message = PlacementRules.Check(refused with { Location = ParameterLocation.Nowhere });

        message.Should().BeNull("écarté, un composite ne pose aucun problème");

        // Et le message que l'invite montrera, obtenu sans construire l'impossible.
        Action building = () => _ = Composite(location);

        string thrown = building.Should().Throw<ArgumentException>().Which.Message;

        thrown.Should().Contain("request")
            .And.Contain("AjouterDocumentCommand")
            .And.Contain(where);
    }

    [Theory]
    [InlineData(ParameterLocation.Body)]
    [InlineData(ParameterLocation.Nowhere)]
    public void A_composite_is_allowed_in_the_body_and_nowhere(ParameterLocation location)
    {
        PlacementRules.Allows(composite: true, location).Should().BeTrue();
        PlacementRules.Check(Composite(location)).Should().BeNull();
    }

    [Fact]
    public void A_scalar_is_allowed_everywhere()
    {
        foreach (ParameterLocation location in Enum.GetValues<ParameterLocation>())
        {
            PlacementRules.Allows(composite: false, location).Should().BeTrue();
        }
    }

    [Fact]
    public void A_placeholder_no_field_fills_is_named_with_its_path()
    {
        AuthoredRoute route = Route("/Dossier/{identifiant}", []);

        PlacementRules.Check(route).Should().ContainSingle()
            .Which.Should().Contain("{identifiant}").And.Contain("/Dossier/{identifiant}");
    }

    [Fact]
    public void A_field_placed_in_a_path_that_does_not_expect_it_is_named()
    {
        // L'autre sens de la même règle, et il compte autant : un champ placé dans le chemin sans que le
        // chemin le prévoie donnerait un paramètre que rien ne reçoit.
        AuthoredRoute route = Route(
            "/Dossier/AjouterDocuments",
            [Scalar("identifiant", ParameterLocation.Path)]);

        PlacementRules.Check(route).Should().ContainSingle()
            .Which.Should().Contain("identifiant").And.Contain("ajoutez");
    }

    [Fact]
    public void A_filled_placeholder_passes()
    {
        AuthoredRoute route = Route(
            "/Dossier/{identifiant}",
            [Scalar("identifiant", ParameterLocation.Path)]);

        PlacementRules.Check(route).Should().BeEmpty();
    }

    [Fact]
    public void Two_parameters_of_one_destination_cannot_share_a_name()
    {
        AuthoredRoute route = Route(
            "/Dossier/Chercher",
            [
                Scalar("depuis", ParameterLocation.Query),
                Scalar("jusqua", ParameterLocation.Query) with { Name = "depuis" },
            ]);

        PlacementRules.Check(route).Should().ContainSingle()
            .Which.Should().Contain("depuis").And.Contain("requête").And.Contain("2 fois");
    }

    [Fact]
    public void The_same_name_in_two_different_destinations_is_allowed()
    {
        // Un en-tête et un paramètre de requête peuvent porter le même nom : ils ne se disputent rien.
        AuthoredRoute route = Route(
            "/Dossier/Chercher",
            [
                Scalar("marche", ParameterLocation.Query),
                Scalar("marche", ParameterLocation.Header),
            ]);

        PlacementRules.Check(route).Should().BeEmpty();
    }

    [Fact]
    public void A_dropped_field_cannot_collide_with_anything()
    {
        AuthoredRoute route = Route(
            "/Dossier/Chercher",
            [
                Scalar("depuis", ParameterLocation.Query),
                Scalar("depuis", ParameterLocation.Nowhere),
            ]);

        PlacementRules.Check(route).Should().BeEmpty("un champ écarté n'occupe aucune place");
    }

    private static FieldPlacement Composite(ParameterLocation location) =>
        new("request", "AjouterDocumentCommand", IsComposite: true, location, Required: false);

    private static FieldPlacement Scalar(string field, ParameterLocation location) =>
        new(field, "string", IsComposite: false, location, Required: false);

    private static AuthoredRoute Route(string path, IReadOnlyList<FieldPlacement> placements) => new(
        "AjouterDocuments",
        path,
        HttpOperationMethod.Post,
        placements,
        [],
        InsertionPoint.AtEnd,
        RouteDerivationKind.Fallback,
        TookSuggestion: false);
}
