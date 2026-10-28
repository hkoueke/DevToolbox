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
