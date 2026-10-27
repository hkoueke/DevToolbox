using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Les formes qui coûtent cher s'arrêtent-elles sur une limite nommée, plutôt que sur une pile vide ?
/// </summary>
/// <remarks>
/// Ces documents ont été écrits avant le lecteur, et éprouvés hostiles sans lui. Ce qu'ils demandent
/// n'est pas que l'outil tienne, mais qu'il <em>dise</em> pourquoi il s'arrête : un échec sans raison
/// envoie chercher un défaut dans le contrat alors qu'il est dans le budget, et une expiration silencieuse
/// est indiscernable d'un outil cassé.
/// <para>
/// Les refus structurels — définition de type de document, traversée, cycle, version — sont éprouvés avec
/// le lecteur, là où ils se décident. Ici ne restent que les formes dont le coût est la seule menace.
/// </para>
/// </remarks>
public sealed class WsdlHostileTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-hostile-" + Guid.NewGuid().ToString("N"));

    private readonly WsdlReader _reader = new();

    public WsdlHostileTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task An_enormous_enumeration_is_read_whole_or_refused_by_a_named_limit()
    {
        // Dix mille valeurs. Aucune raison de refuser une énumération parce qu'elle est longue, aucune
        // raison non plus de la lire sans budget : les deux issues sont bonnes, s'arrêter sans rien dire
        // ne l'est pas.
        string entry = await EnumerationAsync(10_000);

        WsdlReadResult result = await ReadAsync(entry);

        if (result.IsFaithful)
        {
            result.Contract!.Types.Values.Should().Contain(
                type => type.Facets.Count == 10_000, "lue entière, ou pas du tout");
        }
        else
        {
            result.Diagnostics.Should().Contain(
                diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached,
                "refusée, mais en nommant la limite : " + Said(result));
        }
    }

    [Fact]
    public void A_type_with_very_many_attributes_keeps_every_one_of_them()
    {
        WsdlReadResult result = _reader.ReadBytes(
            Encoding.UTF8.GetBytes(GeneratedWsdl.ManyAttributes(2_000)),
            "attributs.wsdl",
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root));

        if (result.IsFaithful)
        {
            result.Contract!.Types.Values.Should().Contain(type => type.Attributes.Count == 2_000);
        }
        else
        {
            result.Diagnostics.Should()
                .Contain(diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached, Said(result));
        }
    }

    [Fact]
    public async Task A_document_count_past_the_budget_stops_on_the_count_and_not_on_the_depth()
    {
        // La chaîne est courte et large : si le refus parlait de profondeur, il désignerait la mauvaise
        // limite, et le développeur réglerait celle qui n'était pas en cause.
        string directory = Path.Combine(_root, "chaine");
        string entry = GeneratedWsdl.Chain(directory, 12);

        WsdlReadResult result = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default with { MaxDocumentCount = 4 },
            ImportPolicy.ConfinedTo(directory),
            CancellationToken.None);

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached
                && diagnostic.Message.Contains("nombre de documents", StringComparison.Ordinal),
            Said(result));
    }

    [Fact]
    public async Task Two_schemas_sharing_a_file_name_read_from_disk_are_perfectly_ordinary()
    {
        // Ce document n'est hostile qu'une fois matérialisé à plat, où le second écraserait le premier.
        // Lu depuis le disque, chacun garde son dossier, et l'outil ne doit pas inventer un problème : le
        // danger appartient à la récupération par adresse, pas à la lecture.
        string entry = Materialise();

        WsdlReadResult result = await ReadAsync(entry);

        result.IsFaithful.Should().BeTrue(Said(result));
        result.Contract!.Documents.Should().HaveCount(3, "le wsdl et les deux homonymes");
        result.Contract.Types.Keys.Should()
            .Contain("http://entreprise.local/homonymes/a:Jetona")
            .And.Contain("http://entreprise.local/homonymes/b:Jetonb");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Écrit le document homonyme et ses deux schémas, chacun dans son dossier.</summary>
    private string Materialise()
    {
        foreach (string branch in new[] { "a", "b" })
        {
            string directory = Path.Combine(_root, "same-name", branch);

            Directory.CreateDirectory(directory);

            File.WriteAllText(
                Path.Combine(directory, "types.xsd"),
                CorpusLoader.Read("Wsdl.hostile.same_name." + branch + ".types.xsd"),
                new UTF8Encoding(false));
        }

        string entry = Path.Combine(_root, "same-name.wsdl");

        File.WriteAllText(
            entry, CorpusLoader.Read("Wsdl.hostile.same-name.wsdl"), new UTF8Encoding(false));

        return entry;
    }

    /// <summary>Écrit l'énumération et le contrat minimal qui l'importe.</summary>
    private async Task<string> EnumerationAsync(int values)
    {
        await File.WriteAllTextAsync(
            Path.Combine(_root, "codes.xsd"), GeneratedWsdl.EnormousEnumeration(values));

        string entry = Path.Combine(_root, "codes.wsdl");

        await File.WriteAllTextAsync(entry, """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:codes">
              <wsdl:types>
                <xs:schema targetNamespace="urn:codes">
                  <xs:import namespace="http://entreprise.local/engendre/codes" schemaLocation="codes.xsd" />
                </xs:schema>
              </wsdl:types>
            </wsdl:definitions>
            """);

        return entry;
    }

    private Task<WsdlReadResult> ReadAsync(string entry) =>
        _reader.ReadAsync(
            entry, WsdlLimits.Default, ImportPolicy.ConfinedTo(_root), CancellationToken.None);

    private static string Said(WsdlReadResult result) =>
        string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
}
