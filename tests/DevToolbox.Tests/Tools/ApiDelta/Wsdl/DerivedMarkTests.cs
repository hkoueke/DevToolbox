using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
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
/// Un élément dérivé et un élément recopié sont-ils distinguables dans le document produit ?
/// </summary>
/// <remarks>
/// C'est la seule promesse que cette tranche ajoute à la précédente, et c'est aussi la seule qu'elle
/// pourrait rompre en silence. Là où une description candidate donne ses propres octets, un contrat de
/// service ne donne qu'une traduction : un relecteur qui ne verrait pas la différence prendrait une
/// décision de mise en production sur une description que personne n'a écrite.
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

        SpecDocument corrected = run.Reread(built.Value.Serialized);

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

            Trace(corrected, mark.Target).Should().Contain(
                ["from", "wsdl", "emplacement", "projection"],
                mark.Target.Display + " doit nommer le document, l'endroit, et ce que la projection "
                    + "valait");

            Value(corrected, mark.Target, "wsdl").Should().BeOneOf(
                opened, "la trace doit nommer un des documents que la lecture a ouverts");
            Value(corrected, mark.Target, "emplacement").Should().Contain("ligne");
            Value(corrected, mark.Target, "projection").Should().BeOneOf("portée", "approchante");
        }
    }

    [Fact]
    public void A_derived_element_never_claims_the_projection_carried_nothing()
    {
        // Un élément écrit a forcément été porté, exactement ou à peu près. Annoncer « non portée » sur
        // un élément présent dirait au lecteur le contraire de ce qu'il a sous les yeux ; ce qui n'a pas
        // été porté est une absence, et une absence se dit par un marqueur, pas par une trace.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("lireDocument"), run.Route("archiverDocument")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Marks.Should().OnlyContain(mark =>
            mark.Derived!.Confidence
                != ProjectionConfidence.NotCarried);
    }

    [Fact]
    public void A_mark_names_the_document_and_never_the_path_it_was_read_from()
    {
        // Un chemin absolu rendrait deux exécutions identiques distinguables, et porterait le dossier
        // personnel du développeur dans un document destiné à être relu. C'est la règle que la
        // déclaration du document suivait déjà, et la trace dérivée n'y échappe pas.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("dossierModifie")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string text = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        text.Should().Contain("catalogue.wsdl");
        text.Should().NotContain(Path.GetTempPath().Replace('\\', '/'));
        text.Should().NotContain("devtoolbox-wsdl-correction-");
    }

    [Fact]
    public void A_copied_element_carries_no_derived_mark()
    {
        // Le chemin de la tranche précédente, inchangé : le contenu vient des octets du candidat, et sa
        // trace dit cela et rien de plus. Une trace qui parlerait de WSDL là où aucun WSDL n'est entré
        // rendrait la marque inutilisable pour distinguer les deux.
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

        SpecReadResult reread = reader.ReadBytes(
            built.Value.Serialized, "corrigé", DocumentRole.Production, LoadLimits.Default);

        reread.Document.Should().NotBeNull();

        Trace(reread.Document, built.Value.Marks[0].Target).Should().Equal("from");
    }

    private static IEnumerable<string> Trace(SpecDocument document, SpecPointer target)
    {
        SpecNode? trace = PointerNavigator.Find(
            document.Root, target.Child(CorrectionMark.Member).Json);

        trace.Should().BeOfType<MappingNode>(target.Display + " doit porter sa trace d'origine");

        return ((MappingNode)trace).Entries.Select(entry => entry.Key.Text);
    }

    private static string Value(SpecDocument document, SpecPointer target, string field)
    {
        SpecNode? node = PointerNavigator.Find(
            document.Root, target.Child(CorrectionMark.Member).Child(field).Json);

        return node.Should().BeOfType<ScalarNode>().Subject.Text;
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
