using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Authoring;

/// <summary>
/// Ce que le verbe permet de proposer, et le cas où il ne permet rien.
/// </summary>
/// <remarks>
/// Le dernier essai de ce fichier est celui qui compte. Un champ composite sous un verbe de lecture n'a
/// pas de placement proposable : la requête est impossible, et le corps serait écrire un « GET » à corps.
/// Il arrive donc « nulle part » — et s'il y arrivait silencieusement, un développeur qui accepte les
/// défauts en bloc perdrait un champ sans l'avoir voulu.
/// </remarks>
public sealed class PlacementDefaultsTests
{
    private static readonly (string Field, string Declared, bool IsComposite)[] Flat =
    [
        ("identifiant", "string", false),
        ("date", "string", false),
    ];

    private static readonly (string Field, string Declared, bool IsComposite)[] Composite =
    [
        ("request", "AjouterDocumentCommand", true),
    ];

    [Theory]
    [InlineData(HttpOperationMethod.Get)]
    [InlineData(HttpOperationMethod.Delete)]
    public void A_reading_verb_proposes_the_query_for_scalars(HttpOperationMethod method)
    {
        IReadOnlyList<FieldPlacement> placements = PlacementDefaults.For(method, Flat);

        placements.Should().HaveCount(2);
        placements.Should().OnlyContain(one => one.Location == ParameterLocation.Query);
        placements.Should().OnlyContain(one => !one.Required, "le contrat ne dit rien de l'obligation");
    }

    [Theory]
    [InlineData(HttpOperationMethod.Post)]
    [InlineData(HttpOperationMethod.Put)]
    [InlineData(HttpOperationMethod.Patch)]
    public void A_writing_verb_proposes_the_body(HttpOperationMethod method)
    {
        PlacementDefaults.For(method, Flat)
            .Should().OnlyContain(one => one.Location == ParameterLocation.Body);

        PlacementDefaults.For(method, Composite)
            .Should().OnlyContain(one => one.Location == ParameterLocation.Body);
    }

    [Fact]
    public void The_order_of_the_contract_is_kept()
    {
        // L'ordre des champs est celui du contrat : un développeur qui lit son WSDL à côté doit retrouver
        // la même suite.
        PlacementDefaults.For(HttpOperationMethod.Get, Flat)
            .Select(one => one.Field).Should().Equal("identifiant", "date");
    }

    [Fact]
    public void No_field_is_proposed_as_required()
    {
        // Rien dans le contrat ne le dit — tout y est minOccurs=0 nillable=true — donc rien ne le propose.
        foreach (HttpOperationMethod method in new[]
            { HttpOperationMethod.Get, HttpOperationMethod.Post, HttpOperationMethod.Delete })
        {
            PlacementDefaults.For(method, Flat).Should().OnlyContain(one => !one.Required);
        }
    }

    [Theory]
    [InlineData(HttpOperationMethod.Get)]
    [InlineData(HttpOperationMethod.Delete)]
    public void A_composite_under_a_reading_verb_is_flagged_rather_than_quietly_dropped(
        HttpOperationMethod method)
    {
        IReadOnlyList<FieldPlacement> placements = PlacementDefaults.For(method, Composite);

        FieldPlacement only = placements.Should().ContainSingle().Which;

        only.Location.Should().Be(
            ParameterLocation.Nowhere, "ni la requête ni le corps ne conviennent sous ce verbe");

        PlacementDefaults.AwaitsDecision(method, only).Should().BeTrue(
            "accepter les défauts en bloc ne doit pas pouvoir écarter ce champ en silence");
    }

    [Fact]
    public void A_deliberate_drop_and_an_undecided_one_are_not_confused()
    {
        // Les deux sont « nulle part ». L'un est une décision du développeur, l'autre l'aveu que l'outil
        // n'en a pas. Les confondre laisserait le second passer pour le premier.
        FieldPlacement chosen = new(
            "categorieDossier", "string", IsComposite: false, ParameterLocation.Nowhere, Required: false);

        PlacementDefaults.AwaitsDecision(HttpOperationMethod.Get, chosen).Should().BeFalse(
            "un scalaire écarté sciemment n'attend aucune décision");

        FieldPlacement undecided = PlacementDefaults
            .For(HttpOperationMethod.Get, Composite).Single();

        PlacementDefaults.AwaitsDecision(HttpOperationMethod.Get, undecided).Should().BeTrue();
    }
}
