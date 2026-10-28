using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Un élément dérivé et un élément recopié sont-ils distinguables une fois la correction bâtie ?
/// </summary>
/// <remarks>
/// C'est la seule promesse que cette tranche ajoute à la précédente, et c'est aussi la seule qu'elle
/// pourrait rompre en silence. Là où une description candidate donne ses propres octets, un contrat de
/// service ne donne qu'une traduction : un relecteur qui ne verrait pas la différence prendrait une
/// décision de mise en production sur une description que personne n'a écrite.
/// <para>
/// La distinction vivait dans le document produit, sous « x-apidelta-origin ». Elle vit maintenant dans
/// le registre que la construction rend, et que le rapport publie : le fichier écrit est destiné à
/// devenir un contrat publié et ne porte que de l'OpenAPI. Ces essais tiennent donc la promesse là où
/// elle est désormais tenue, et vérifient en plus que le document n'en porte plus rien.
/// </para>
/// </remarks>
public sealed class DerivedMarkTests
{
    [Fact]
    public void Every_derived_element_names_its_document_its_place_and_what_the_projection_was_worth()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        CorrectionSelection selection = new([run.Route("dossierModifie"), run.Route("lireDocument")]);
        Result<BuiltCorrection> built = run.Build(selection);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Marks.Should().NotBeEmpty();

        // Le document, et non le point d'entrée : un contrat est un graphe de fichiers, et un type vient
        // du schéma qui le déclare. Une trace qui nommerait toujours le WSDL ferait chercher dans le
        // mauvais fichier.
        IReadOnlyList<string> opened =
            [.. run.Contract.Documents.Select(document => Path.GetFileName(document.Path))];

        opened.Should().HaveCountGreaterThan(1, "le contrat du corpus est un graphe");

        foreach (CorrectionMark mark in built.Value.Marks)
        {
            mark.IsDerived.Should().BeTrue(
                mark.Target.Display + " vient du contrat, et doit le dire");

            DerivedOrigin origin = mark.Derived!;

            origin.Document.Should().BeOneOf(
                opened, "la trace doit nommer un des documents que la lecture a ouverts");
            origin.At.Should().Contain("ligne", "une trace sans endroit ne se vérifie pas");
            origin.Worth.Should().BeOneOf("portée", "approchante");
        }
    }

    [Fact]
    public void A_derived_element_never_claims_the_projection_carried_nothing()
    {
        // Un élément écrit a forcément été porté, exactement ou à peu près. Annoncer « non portée » sur
        // un élément présent dirait au lecteur le contraire de ce qu'il a sous les yeux ; ce qui n'a pas
        // été porté est une absence, et une absence se dit comme un endroit non rempli.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("lireDocument"), run.Route("archiverDocument")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Marks.Should().OnlyContain(mark =>
            mark.Derived!.Confidence
                != ProjectionConfidence.NotCarried);
    }

    [Fact]
    public void A_trace_names_the_document_and_never_the_path_it_was_read_from()
    {
        // Un chemin absolu rendrait deux exécutions identiques distinguables, et porterait le dossier
        // personnel du développeur dans ce qui sera relu et validé.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("dossierModifie")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Marks.Should().Contain(mark =>
            string.Equals(mark.Derived!.Document, "catalogue.wsdl", StringComparison.Ordinal));

        foreach (CorrectionMark mark in built.Value.Marks)
        {
            string named = mark.Derived!.Document;

            named.Should().NotContain("/", "un nom de fichier, et non un chemin");
            named.Should().NotContain("\\");
            named.Should().NotContain("devtoolbox-wsdl-correction-");
        }
    }

    [Fact]
    public void The_written_document_carries_no_trace_of_the_tool()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("dossierModifie")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string text = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        text.Should().NotContain("x-apidelta");
        text.Should().NotContain("catalogue.wsdl", "le contrat ne nomme pas le fichier dont il est tiré");
        text.Should().NotContain(Path.GetTempPath().Replace('\\', '/'));
    }

    [Fact]
    public void A_copied_element_is_recorded_as_copied()
    {
        // Le chemin de la tranche précédente, inchangé : le contenu vient des octets du candidat, et le
        // registre dit cela et rien de plus. Un registre qui parlerait de WSDL là où aucun WSDL n'est
        // entré rendrait la distinction inutilisable.
        SpecReader reader = new(NullLogger<SpecReader>.Instance);
        CorrectionBuilder builder = new(
            new OperationCatalog(), new SplicingEmitter(new CanonicalEmitter()));

        SpecDocument production = Read(reader, "baseline-30.yaml", DocumentRole.Production);
        SpecDocument candidate = Read(reader, "candidate-additive.yaml", DocumentRole.Candidate);
        OperationSelector route = new("/dossiers/{id}", HttpOperationMethod.Get);

        Result<BuiltCorrection> built = builder.Build(new CorrectionRequest(
            production,
            candidate,
            [],
            new CorrectionSelection([route]),
            DependencyClosure.Empty,
            Version: null,
            CanonicalFormatting.Default));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Marks.Should().OnlyContain(mark => !mark.IsDerived);

        Encoding.UTF8.GetString(built.Value.Serialized.Span)
            .Should().NotContain("x-apidelta");
    }

    private static SpecDocument Read(SpecReader reader, string fileName, DocumentRole role)
    {
        SpecReadResult result = reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read(fileName)), fileName, role, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            fileName + " doit se lire : "
            + string.Join(" / ", result.Diagnostics.Select(one => one.Message)));

        return result.Document;
    }
}
