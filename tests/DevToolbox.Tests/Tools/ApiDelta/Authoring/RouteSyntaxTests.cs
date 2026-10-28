using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Authoring;

/// <summary>
/// Ce qu'un chemin tapé doit satisfaire avant de servir à quoi que ce soit.
/// </summary>
/// <remarks>
/// Avant, et c'est l'objet du dernier essai de ce fichier : les segments d'un <c>SpecPointer</c> sont
/// échappés au format JSON Pointer, où « ~ » est le caractère d'échappement. Un chemin qui en porte
/// construirait un pointeur désignant un autre nœud que celui visé, et l'épissure écrirait là. La règle
/// vient d'une passe STRIDE et n'est pas théorique : un gabarit de chemin contient des « / » par
/// construction.
/// </remarks>
public sealed class RouteSyntaxTests
{
    [Theory]
    [InlineData("/Dossier/AjouterDocuments")]
    [InlineData("/Dossier/{identifiant}")]
    [InlineData("/Dossier/{identifiant}/Documents")]
    [InlineData("/Dossier/{dossier}/Documents/{document}")]
    [InlineData("/")]
    public void A_well_formed_path_is_accepted(string typed)
    {
        Result<string> read = RouteSyntax.Path(typed);

        read.IsSuccess.Should().BeTrue(read.Failure?.Message);
        read.Value.Should().Be(typed);
    }

    [Theory]
    [InlineData("Dossier/Ajouter", "commence par")]
    [InlineData("/Dossier/Ajouter Documents", "espace")]
    [InlineData("/Dossier//Documents", "segment vide")]
    [InlineData("/Dossier/{id", "n'est pas fermée")]
    [InlineData("/Dossier/id}", "n'a pas d'ouvrante")]
    [InlineData("/Dossier/{}", "porte un nom")]
    [InlineData("/Dossier/{a}/{a}", "deux fois")]
    [InlineData("/Dossier/{a{b}}", "s'imbrique")]
    [InlineData("", "ne peut pas être vide")]
    public void A_malformed_path_is_refused_by_naming_the_rule(string typed, string expected)
    {
        Result<string> read = RouteSyntax.Path(typed);

        read.IsSuccess.Should().BeFalse("« " + typed + " » enfreint une règle");
        read.Failure!.Reason.Should().Be(FailureReason.InvalidInput);
        read.Failure.Message.Should().Contain(
            expected, "un refus qui ne nomme pas sa règle ne se corrige pas");
    }

    [Fact]
    public void A_path_is_never_repaired_in_silence()
    {
        // Un chemin corrigé en silence serait un chemin que le développeur n'a pas choisi, ce qui est
        // exactement ce que cette fonctionnalité existe pour empêcher.
        RouteSyntax.Path("Dossier/Ajouter").IsSuccess.Should().BeFalse(
            "la barre manquante n'est pas ajoutée pour nous");

        RouteSyntax.Path("/Dossier/Ajouter Documents").IsSuccess.Should().BeFalse(
            "l'espace n'est pas retiré pour nous");
    }

    [Theory]
    [InlineData("GET", HttpOperationMethod.Get)]
    [InlineData("post", HttpOperationMethod.Post)]
    [InlineData(" Patch ", HttpOperationMethod.Patch)]
    public void A_verb_openapi_defines_is_accepted(string typed, HttpOperationMethod expected)
    {
        Result<HttpOperationMethod> read = RouteSyntax.Method(typed);

        read.IsSuccess.Should().BeTrue(read.Failure?.Message);
        read.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("FETCH")]
    [InlineData("")]
    [InlineData("GET POST")]
    public void A_verb_openapi_does_not_define_is_refused_by_listing_those_it_does(string typed)
    {
        Result<HttpOperationMethod> read = RouteSyntax.Method(typed);

        read.IsSuccess.Should().BeFalse();
        read.Failure!.Message.Should().Contain("GET").And.Contain("PATCH");
    }

    [Fact]
    public void The_placeholders_of_a_path_are_read_in_order()
    {
        RouteSyntax.Placeholders("/Dossier/{dossier}/Documents/{document}")
            .Should().Equal("dossier", "document");

        RouteSyntax.Placeholders("/Dossier/AjouterDocuments").Should().BeEmpty();
    }

    [Fact]
    public void A_path_carrying_a_pointer_escape_is_refused_before_a_pointer_is_built_from_it()
    {
        // L'essai de la passe STRIDE. « ~ » est le caractère d'échappement du format JSON Pointer : « ~1 »
        // s'y lit comme « / » et « ~0 » comme « ~ ». Un chemin le portant construirait donc un pointeur
        // qui ne désigne pas le nœud visé, et l'épissure écrirait ailleurs que là où le développeur a
        // demandé.
        const string Hostile = "/Dossier~1Documents";

        Result<string> read = RouteSyntax.Path(Hostile);

        read.IsSuccess.Should().BeFalse("ce chemin ne doit jamais atteindre un pointeur");
        read.Failure!.Message.Should().Contain("pointeur");

        // Et la raison, montrée plutôt qu'affirmée : construit malgré tout, ce pointeur désigne un autre
        // emplacement que celui que son texte laisse croire.
        SpecPointer built = SpecPointer.Root.Child("paths").Child(Hostile);

        built.Json.Should().NotContain(
            Hostile, "le segment est réécrit par l'échappement, donc ne désigne plus le même chemin");
    }
}
