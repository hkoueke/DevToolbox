using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Extraction;

/// <summary>
/// Ce qu'un extrait contient, ce qu'il ne contient pas, et ce qu'il garde de son contexte.
/// </summary>
public sealed class ExtractTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-extrait-" + Guid.NewGuid().ToString("N"));

    public ExtractTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task One_operation_taken_from_a_shared_path_yields_only_that_method()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        OperationSelector selector = new("/documents", HttpOperationMethod.Get);

        ExtractResult result = ExtractHarness.Extract(source, [selector]).Value;

        result.Verification.Established.Should().BeTrue(result.Verification.Explanation);

        SpecDocument extract = ExtractHarness.Reread(result);

        Methods(extract, "/documents").Should().Equal("get");

        // Les autres chemins de la source ne sont pas là.
        Paths(extract).Should().Equal("/documents");
    }

    [Fact]
    public async Task The_parameters_an_operation_inherits_from_its_path_are_present()
    {
        // Les omettre produirait un fichier qui se relit, qui se valide, et qui décrit autre chose.
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Get)]).Value;

        SpecDocument extract = ExtractHarness.Reread(result);

        PointerNavigator.Find(extract.Root, "/paths/~1documents/parameters")
            .Should().BeOfType<SequenceNode>()
            .Which.Items.Should().NotBeEmpty("le paramètre du niveau du chemin gouverne l'opération");

        result.InheritedContext.Should().ContainSingle()
            .Which.PathParameters.Should().Contain("X-serviceEmetteur");
    }

    [Fact]
    public async Task The_global_security_requirement_and_only_the_scheme_it_names_are_carried()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Get)]).Value;

        SpecDocument extract = ExtractHarness.Reread(result);

        PointerNavigator.Find(extract.Root, "/security").Should().NotBeNull();

        MappingNode schemes = PointerNavigator
            .Find(extract.Root, "/components/securitySchemes")
            .Should().BeOfType<MappingNode>().Subject;

        schemes.Entries.Select(entry => entry.Key.Text).Should().Equal("bearerAuth");

        result.InheritedContext[0].Security.Should().Contain("bearerAuth");
    }

    [Fact]
    public async Task Every_schema_in_the_chain_appears_exactly_once()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Get)]).Value;

        SpecDocument extract = ExtractHarness.Reread(result);

        MappingNode schemas = PointerNavigator
            .Find(extract.Root, "/components/schemas")
            .Should().BeOfType<MappingNode>().Subject;

        schemas.Entries.Select(entry => entry.Key.Text).Should().OnlyHaveUniqueItems();
        schemas.Entries.Select(entry => entry.Key.Text).Should().Contain("Document");

        result.Closure.Required.Should().NotBeEmpty();
    }

    [Fact]
    public async Task No_unselected_component_comes_along()
    {
        // Un extrait qui emporterait tout « components » par prudence serait aussi illisible que la
        // source : c'est la moitié de la promesse.
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        MappingNode all = PointerNavigator
            .Find(source.Root, "/components/schemas")
            .Should().BeOfType<MappingNode>().Subject;

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Get)]).Value;

        SpecDocument extract = ExtractHarness.Reread(result);

        MappingNode kept = PointerNavigator
            .Find(extract.Root, "/components/schemas")
            .Should().BeOfType<MappingNode>().Subject;

        kept.Entries.Count.Should().BeLessThan(
            all.Entries.Count, "seule la fermeture de l'opération retenue vient");
    }

    [Fact]
    public async Task The_version_is_carried_exactly_as_the_source_wrote_it()
    {
        // Un extrait n'est pas une nouvelle version de l'interface : incrémenter au passage ferait d'une
        // vue un changement.
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        string? before = Text(source, "/info/version");

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Get)]).Value;

        Text(ExtractHarness.Reread(result), "/info/version").Should().Be(before);
    }

    [Fact]
    public async Task The_source_is_byte_identical_afterwards()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        string before = Hash(await File.ReadAllBytesAsync(source.Path));

        ExtractHarness.Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Get)])
            .IsSuccess.Should().BeTrue();

        Hash(await File.ReadAllBytesAsync(source.Path)).Should().Be(before);
    }

    [Fact]
    public async Task The_provenance_stamp_says_the_description_is_partial()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Post)]).Value;

        string text = ExtractHarness.Text(result);

        text.Should().Contain(ProvenanceStamp.Field)
            .And.Contain("Description partielle")
            .And.Contain("POST /documents")
            .And.Contain(source.ContentHash);
    }

    [Fact]
    public async Task The_stamp_can_be_turned_off_and_then_nothing_is_added()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        ExtractResult result = ExtractHarness.Extract(
            source,
            [new OperationSelector("/documents", HttpOperationMethod.Post)],
            stamp: false).Value;

        ExtractHarness.Text(result).Should().NotContain(ProvenanceStamp.Field);
        result.Verification.Established.Should().BeTrue(result.Verification.Explanation);
    }

    [Fact]
    public async Task Only_the_tag_definitions_the_selection_uses_are_carried()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/documents", HttpOperationMethod.Get)]).Value;

        result.CarriedTags.Should().Equal("Documents");

        SpecDocument extract = ExtractHarness.Reread(result);

        if (PointerNavigator.Find(extract.Root, "/tags") is SequenceNode tags)
        {
            tags.Items.OfType<MappingNode>()
                .Select(tag => (tag.Find("name") as ScalarNode)?.Text)
                .Should().Equal("Documents");
        }
    }

    [Fact]
    public async Task Two_operations_of_the_same_path_come_out_together_and_alone()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        ExtractResult result = ExtractHarness.Extract(
            source,
            [
                new OperationSelector("/documents", HttpOperationMethod.Get),
                new OperationSelector("/documents", HttpOperationMethod.Post),
            ]).Value;

        result.Verification.Established.Should().BeTrue(result.Verification.Explanation);

        Methods(ExtractHarness.Reread(result), "/documents")
            .Should().BeEquivalentTo("get", "post");
    }

    [Fact]
    public async Task Every_selectable_operation_of_the_corpus_extracts_and_verifies_on_its_own()
    {
        // La passe exhaustive : chaque opération du corpus, une exécution chacune. C'est le seul essai
        // qui établit que la correction ne dépend pas de l'opération choisie.
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        IReadOnlyList<SelectableOperation> catalogue = ExtractHarness.Catalogue(source);

        catalogue.Should().NotBeEmpty();

        foreach (SelectableOperation operation in catalogue)
        {
            ExtractResult result = ExtractHarness
                .Extract(source, [operation.Selector]).Value;

            result.Verification.Established.Should().BeTrue(
                "{0} doit s'extraire et se vérifier : {1}",
                operation.Selector.Display,
                result.Verification.Explanation);

            SpecDocument extract = ExtractHarness.Reread(result);

            Paths(extract).Should().Equal(operation.Selector.Path);
            Methods(extract, operation.Selector.Path)
                .Should().Equal(operation.Selector.MethodKey);

            Text(extract, "/info/version").Should().Be(Text(source, "/info/version"));
        }
    }

    [Fact]
    public async Task The_catalogue_carries_short_strings_and_nothing_from_the_content()
    {
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        IReadOnlyList<SelectableOperation> catalogue = ExtractHarness.Catalogue(source);

        catalogue.Should().NotBeEmpty();
        catalogue.Should().OnlyHaveUniqueItems();

        SelectableOperation listing = catalogue.Should().Contain(operation =>
            operation.Selector.Path == "/documents"
            && operation.Selector.Method == HttpOperationMethod.Get).Which;

        listing.Summary.Should().Be("Liste les documents");
        listing.Tags.Should().Equal("Documents");
        listing.Display.Should().Be("GET /documents — Liste les documents");
    }

    [Fact]
    public async Task An_operation_declaring_its_own_parameter_masks_the_path_one()
    {
        // Les règles d'OpenAPI : le couple nom-emplacement décide. Fusionner sans cette règle rendrait
        // deux paramètres là où le document n'en voit qu'un.
        SpecDocument source = await ExtractHarness.WrittenAsync(_root, Masking);

        OperationSelector selector = new("/un", HttpOperationMethod.Get);

        EffectiveOperation.InheritedParameters(source, selector)
            .Should().BeEmpty("l'opération redéclare le même couple nom-emplacement");

        MappingNode effective = EffectiveOperation.Of(source, selector)!;

        (effective.Find("parameters") as SequenceNode)!.Items.Should().HaveCount(1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Hash(byte[] content) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));

    private static string? Text(SpecDocument document, string pointer) =>
        (PointerNavigator.Find(document.Root, pointer) as ScalarNode)?.Text;

    private static IReadOnlyList<string> Paths(SpecDocument document) =>
        PointerNavigator.Find(document.Root, "/paths") is MappingNode paths
            ? [.. paths.Entries.Select(entry => entry.Key.Text)]
            : [];

    private static IReadOnlyList<string> Methods(SpecDocument document, string path) =>
        PointerNavigator.Find(document.Root, "/paths/" + path.Replace("/", "~1", StringComparison.Ordinal))
            is MappingNode item
            ? [.. item.Entries.Select(entry => entry.Key.Text).Where(PathRows.Methods.Contains)]
            : [];

    private const string Masking = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            parameters:
              - name: X-appelant
                in: header
                required: false
                schema:
                  type: string
            get:
              operationId: lireUn
              parameters:
                - name: X-appelant
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
        """;
}
