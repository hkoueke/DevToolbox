using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Correction;

/// <summary>
/// Le pari sur lequel repose la tranche 004 : l'épisseur accepte-t-il un nœud qui n'a jamais été écrit ?
/// </summary>
/// <remarks>
/// Une description projetée depuis un WSDL n'a pas d'étendue d'octets : ses nœuds n'ont jamais existé dans
/// un fichier. Si l'épisseur en dépendait, toute l'estimation de 004 serait fausse — et il vaut mieux le
/// savoir en une heure qu'en une semaine.
/// </remarks>
public sealed class SyntheticGraftTests
{
    private readonly SpecReader _reader = new(NullLogger<SpecReader>.Instance);

    [Fact]
    public void A_node_that_was_never_written_grafts_into_production_and_the_result_reparses()
    {
        SpecDocument production = Read("baseline-30.yaml");
        SpecPointer at = SpecPointer.Root.Child("paths").Child("/sante");

        // Monté à la main, sans aucune étendue : exactement ce qu'une projection produira.
        MappingNode synthetic = TreeEdits.Mapping(
            at.Child("get"),
            [
                new MappingEntry(TreeEdits.Key("operationId"), TreeEdits.Key("lireSante")),
                new MappingEntry(TreeEdits.Key("responses"), TreeEdits.Mapping(
                    at.Child("get").Child("responses"),
                    [
                        new MappingEntry(TreeEdits.Key("'200'"), TreeEdits.Mapping(
                            at.Child("get").Child("responses").Child("200"),
                            [new MappingEntry(TreeEdits.Key("description"), TreeEdits.Key("En vie."))])),
                    ])),
            ]);

        synthetic.Span.Length.Should().Be(0, "un nœud synthétique n'occupe aucun octet d'origine");

        Result<ReadOnlyMemory<byte>> spliced = new SplicingEmitter(new CanonicalEmitter()).Splice(
            production,
            [
                new Splice(
                    SpliceKind.Add,
                    SpecPointer.Root.Child("paths"),
                    "/sante",
                    TreeEdits.Mapping(at, [new MappingEntry(TreeEdits.Key("get"), synthetic)])),
            ],
            CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeTrue(spliced.Failure?.Message);

        SpecReadResult reread = _reader.ReadBytes(
            spliced.Value, "corrigé", DocumentRole.Production, LoadLimits.Default);

        reread.Document.Should().NotBeNull(
            "le document épissé doit se relire : "
            + string.Join(" / ", reread.Diagnostics.Select(d => d.Message)));

        PointerNavigator.Find(reread.Document.Root, "/paths/~1sante/get/operationId")
            .Should().NotBeNull("la route synthétique doit être là");

        // Et la production, octet pour octet, hors de l'insertion.
        string before = Encoding.UTF8.GetString(production.Source.Span);
        string after = Encoding.UTF8.GetString(spliced.Value.Span);

        foreach (string line in before.Split('\n').Where(line => line.Trim().Length > 0))
        {
            after.Should().Contain(line.TrimEnd('\r'), "« " + line.Trim() + " » venait de la production");
        }
    }

    private SpecDocument Read(string fileName)
    {
        SpecReadResult result = _reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read(fileName)), fileName,
            DocumentRole.Production, LoadLimits.Default);

        result.Document.Should().NotBeNull();

        return result.Document;
    }
}
