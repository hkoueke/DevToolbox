using DevToolbox.Tools.ApiDelta.Core.Projection;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

/// <summary>
/// La dérivation est-elle déterministe, et dit-elle ce qu'elle a fait ?
/// </summary>
/// <remarks>
/// Le troisième cas est celui qui compte. Un repli annoncé comme une dérivation ferait croire que l'outil
/// a reconnu quelque chose, et un développeur passerait plus vite sur une proposition qu'il aurait dû
/// regarder — ce qui est précisément la façon dont une route fausse arriverait en production.
/// </remarks>
public sealed class RouteDerivationTests
{
    [Theory]
    [InlineData("deposerDocument", "POST", "/documents")]
    [InlineData("lireDocument", "GET", "/documents")]
    [InlineData("modifierDossier", "PUT", "/dossiers")]
    [InlineData("supprimerPiece", "DELETE", "/pieces")]
    [InlineData("corrigerDocument", "PATCH", "/documents")]
    [InlineData("rechercherDossier", "GET", "/dossiers")]
    [InlineData("archiverDocument", "DELETE", "/documents")]
    public void A_known_verb_and_a_subject_derive_both_the_method_and_the_path(
        string operation, string method, string path)
    {
        DerivedRoute route = RouteDerivation.Derive(operation);

        route.Kind.Should().Be(RouteDerivationKind.Derived);
        route.Method.Should().Be(method);
        route.Path.Should().Be(path);
        route.Verb.Should().NotBeNull("le rapport doit pouvoir dire sur quoi il s'est fondé");
    }

    [Fact]
    public void An_unknown_verb_still_yields_a_path_but_the_derivation_says_it_is_partial()
    {
        DerivedRoute route = RouteDerivation.Derive("ventilerEcriture");

        route.Kind.Should().Be(RouteDerivationKind.Partial);
        route.Method.Should().Be("POST", "faute de verbe reconnu");
        route.Path.Should().Be("/ecritures", "le sujet, lui, se dégage du nom");
        route.Verb.Should().BeNull("aucun verbe n'a été reconnu, et le rapport ne doit pas en inventer");
    }

    [Fact]
    public void A_single_word_operation_is_a_fallback_and_is_called_one()
    {
        // « lister » tout court ne dit pas quoi lister. Même si le mot est un verbe connu, il n'y a pas
        // de sujet, donc pas de chemin à dériver : c'est un repli.
        DerivedRoute route = RouteDerivation.Derive("lister");

        route.Kind.Should().Be(RouteDerivationKind.Fallback);
        route.Method.Should().Be("POST");
        route.Path.Should().Be("/lister", "le nom de l'opération, tel quel");
    }

    [Fact]
    public void A_name_with_no_recognisable_shape_is_a_fallback()
    {
        DerivedRoute route = RouteDerivation.Derive("Op42");

        route.Kind.Should().Be(RouteDerivationKind.Fallback);
        route.Path.Should().Be("/op42");
    }

    [Fact]
    public void The_verb_table_is_configurable_and_the_fallback_is_not()
    {
        // La table se configure, parce qu'une maison a son vocabulaire. Le repli ne se configure pas :
        // un service de forme RPC n'a aucune forme REST à trouver, et en inventer une serait décider de
        // ce que l'outil ne peut pas savoir.
        Dictionary<string, string> maison = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ventiler"] = "POST",
            ["solder"] = "DELETE",
        };

        RouteDerivation.Derive("ventilerEcriture", maison).Kind
            .Should().Be(RouteDerivationKind.Derived, "le verbe de la maison est désormais reconnu");

        RouteDerivation.Derive("solderCompte", maison).Method.Should().Be("DELETE");

        // Et le verbe par défaut n'est plus reconnu : la table remplace, elle ne complète pas, afin
        // qu'une maison puisse aussi retirer une correspondance qui la gêne.
        RouteDerivation.Derive("lireDocument", maison).Kind
            .Should().Be(RouteDerivationKind.Partial);
    }

    [Fact]
    public void A_run_of_capitals_counts_as_one_word()
    {
        DerivedRoute route = RouteDerivation.Derive("lireURLDocument");

        route.Kind.Should().Be(RouteDerivationKind.Derived);
        route.Method.Should().Be("GET");
        route.Path.Should().Be("/urldocuments", "« URL » n'est pas découpé lettre par lettre");
    }

    [Theory]
    [InlineData("deposer-document")]
    [InlineData("deposer_document")]
    [InlineData("deposerDocument")]
    public void The_three_naming_conventions_are_read_the_same_way(string operation)
    {
        DerivedRoute route = RouteDerivation.Derive(operation);

        route.Display.Should().Be(
            "POST /documents", "les trois conventions se rencontrent dans le même contrat");
    }

    [Fact]
    public void A_subject_already_plural_does_not_gain_a_second_s()
    {
        RouteDerivation.Derive("lireDocuments").Path.Should().Be("/documents");
    }

    [Fact]
    public void The_same_name_always_derives_the_same_route()
    {
        // Déterministe : le rapport, le point de reprise et la proposition doivent dire la même chose
        // d'une exécution à l'autre, sans quoi reprendre une séance en changerait le sens.
        DerivedRoute first = RouteDerivation.Derive("deposerDocument");
        DerivedRoute second = RouteDerivation.Derive("deposerDocument");

        second.Should().Be(first);
    }
}
