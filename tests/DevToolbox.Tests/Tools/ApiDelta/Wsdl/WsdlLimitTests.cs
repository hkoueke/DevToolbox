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
/// Chaque limite configurée, atteinte, est-elle rapportée en nommant la limite ?
/// </summary>
/// <remarks>
/// La leçon vient de la tranche précédente, où une chaîne de références plausible avait fait déborder la
/// pile : sans diagnostic, sans position, sans rien dire de ce qui venait d'être lu. Un budget qui
/// s'arrête sans se nommer envoie chercher un défaut dans le contrat alors qu'il est dans la
/// configuration — et le développeur règle la limite qui n'était pas en cause.
/// <para>
/// Les épreuves hostiles éprouvent la <em>tenue</em> sur des formes coûteuses. Celles-ci éprouvent autre
/// chose : que chaque budget, serré exprès jusqu'à mordre, se nomme en mordant.
/// </para>
/// </remarks>
public sealed class WsdlLimitTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-budget-" + Guid.NewGuid().ToString("N"));

    private readonly WsdlReader _reader = new();

    public WsdlLimitTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_document_larger_than_its_budget_is_refused_by_naming_the_size()
    {
        string entry = await OrdinaryAsync().ConfigureAwait(true);

        WsdlReadResult result = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default with { MaxDocumentBytes = 512 },
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).ConfigureAwait(true);

        Named(result, "taille");
    }

    [Fact]
    public async Task An_import_chain_deeper_than_its_budget_is_refused_by_naming_the_chain()
    {
        string directory = Path.Combine(_root, "chaine");
        string entry = GeneratedWsdl.Chain(directory, 12);

        WsdlReadResult result = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default with { MaxImportChainDepth = 3 },
            ImportPolicy.ConfinedTo(directory),
            CancellationToken.None).ConfigureAwait(true);

        Named(result, "chaîne");
    }

    [Fact]
    public async Task A_document_count_past_its_budget_is_refused_by_naming_the_count()
    {
        string directory = Path.Combine(_root, "compte");
        string entry = GeneratedWsdl.Chain(directory, 12);

        WsdlReadResult result = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default with { MaxDocumentCount = 4 },
            ImportPolicy.ConfinedTo(directory),
            CancellationToken.None).ConfigureAwait(true);

        Named(result, "nombre de documents");
    }

    [Fact]
    public async Task An_enumeration_past_its_budget_is_refused_by_naming_the_enumeration()
    {
        // L'énumération vit dans un schéma, qu'un WSDL importe : c'est la forme réelle, et c'est aussi
        // la seule par laquelle le budget d'énumération est atteint.
        string directory = Path.Combine(_root, "codes");

        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "codes.xsd"),
            GeneratedWsdl.EnormousEnumeration(500),
            new UTF8Encoding(false)).ConfigureAwait(true);

        string entry = Path.Combine(directory, "codes.wsdl");

        await File.WriteAllTextAsync(
            entry,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:codes">
              <wsdl:types>
                <xs:schema targetNamespace="urn:codes">
                  <xs:import namespace="http://entreprise.local/engendre/codes"
                             schemaLocation="codes.xsd" />
                </xs:schema>
              </wsdl:types>
            </wsdl:definitions>
            """,
            new UTF8Encoding(false)).ConfigureAwait(true);

        WsdlReadResult result = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default with { MaxEnumerationValues = 50 },
            ImportPolicy.ConfinedTo(directory),
            CancellationToken.None).ConfigureAwait(true);

        Named(result, "valeurs");
    }

    [Fact]
    public async Task A_contract_within_every_budget_is_read_without_a_word_about_limits()
    {
        // Le revers, et il compte autant : un budget qui se plaindrait sans mordre apprendrait à
        // l'équipe à ne plus lire les refus de budget.
        string entry = await OrdinaryAsync().ConfigureAwait(true);

        WsdlReadResult result = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).ConfigureAwait(true);

        result.IsFaithful.Should().BeTrue(Said(result));
        result.Diagnostics.Should().NotContain(
            diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Établit que la lecture a été refusée, et que le refus nomme la limite en cause.</summary>
    private static void Named(WsdlReadResult result, string limit)
    {
        result.IsFaithful.Should().BeFalse("ce budget est serré pour mordre");

        result.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached
                && diagnostic.Message.Contains(limit, StringComparison.OrdinalIgnoreCase),
            "le refus doit nommer « " + limit + " » : " + Said(result));
    }

    private async Task<string> OrdinaryAsync()
    {
        foreach (string name in new[] { "catalogue.wsdl", "catalogue-types.xsd", "commun.xsd" })
        {
            await File.WriteAllTextAsync(
                Path.Combine(_root, name),
                CorpusLoader.Read("Wsdl." + name),
                new UTF8Encoding(false)).ConfigureAwait(true);
        }

        return Path.Combine(_root, "catalogue.wsdl");
    }

    private static string Said(WsdlReadResult result) =>
        string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
}
