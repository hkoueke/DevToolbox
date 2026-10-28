using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Loading;

/// <summary>
/// Ce que la validation accepte, ce qu'elle refuse, et ce qu'elle laisse intact.
/// </summary>
public sealed class ValidationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-validation-" + Guid.NewGuid().ToString("N"));

    public ValidationTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("baseline-30.yaml")]
    [InlineData("baseline-30.json")]
    [InlineData("baseline-31.yaml")]
    public async Task A_valid_description_passes_validation(string fileName)
    {
        SpecDocument document = await ReadAsync(fileName);

        IReadOnlyList<Diagnostic> found = new OpenApiSemanticAdapter().Validate(document);

        found.Should().NotContain(
            diagnostic => diagnostic.IsBlocking,
            string.Join(" · ", found.Select(diagnostic => diagnostic.Message)));
    }

    [Fact]
    public async Task Unknown_vendor_extensions_and_schema_keywords_are_not_rejected()
    {
        // La trouvaille de la reconnaissance prévue au plan : la bibliothèque expose
        // IOpenApiSchema.UnrecognizedKeywords et IOpenApiExtensible.Extensions, donc elle conserve l'un
        // et l'autre. Ce qui est vérifié ici est ce dont l'outil dépend vraiment : qu'elle ne refuse pas un
        // document qui en porte. Que l'outil n'écrive jamais depuis son modèle reste la raison pour
        // laquelle cette réponse ne nous engage pas.
        SpecDocument document = await ReadAsync("baseline-31.yaml");

        new OpenApiSemanticAdapter().Validate(document).Should().NotContain(
            diagnostic => diagnostic.IsBlocking);

        // Et dans notre arbre, les deux sont là, écrits comme ils l'étaient.
        MappingNode root = (MappingNode)document.Root;
        SpecNode? unknownKeyword = PointerNavigator.Find(
            root, "/components/schemas/Document/properties/inconnu/x-sg-format");

        unknownKeyword.Should().BeOfType<ScalarNode>();
        ((ScalarNode)unknownKeyword).Text.Should().Be("interne");
    }

    [Fact]
    public async Task A_structural_violation_is_reported_where_it_is()
    {
        string path = Write(
            "structure.yaml",
            """
            openapi: 3.0.3
            info:
              title: Sans version d'info
            paths:
              /documents:
                get:
                  responses: {}
            """);

        SpecDocument document = await ReadAsync(path, fromCorpus: false);

        IReadOnlyList<Diagnostic> found = new OpenApiSemanticAdapter().Validate(document);

        found.Should().Contain(diagnostic => diagnostic.IsBlocking);
        found.Should().OnlyContain(diagnostic => diagnostic.Position != null);
    }

    [Fact]
    public async Task Every_use_of_a_dead_reference_is_named()
    {
        SpecDocument document = await ReadAsync("broken-unresolved-ref.yaml");

        List<Diagnostic> found = [];
        ReferenceGraphResult graph = new ReferenceGraph().Resolve(
            document, ReferencePolicy.Closed, LoadLimits.Default, found);

        graph.Unresolved.Should().HaveCount(3, "la référence morte est employée à trois endroits");

        Diagnostic refusal = found.Single(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.UnresolvedReference, StringComparison.Ordinal));

        // Un diagnostic pour la référence, et les trois endroits dedans : nommer le premier seulement
        // obligerait le développeur à chercher les autres.
        refusal.Message.Should().Contain("3");
        refusal.Related.Should().HaveCount(2);
    }

    [Fact]
    public async Task Legal_cycles_are_recorded_rather_than_reported()
    {
        SpecDocument document = await ReadAsync("cyclic.yaml");

        List<Diagnostic> found = [];
        ReferenceGraphResult graph = new ReferenceGraph().Resolve(
            document, ReferencePolicy.Closed, LoadLimits.Default, found);

        graph.Cycles.Should().NotBeEmpty("un dossier qui contient un dossier est une description juste");
        found.Should().BeEmpty();
    }

    [Fact]
    public async Task An_external_reference_is_refused_while_the_policy_is_closed()
    {
        string path = Write(
            "externe.yaml",
            """
            openapi: 3.0.3
            info:
              title: Référence externe
              version: 1.0.0
            paths:
              /documents:
                get:
                  operationId: listerDocuments
                  responses:
                    '200':
                      description: Les documents.
                      content:
                        application/json:
                          schema:
                            $ref: 'commun.yaml#/components/schemas/Document'
            """);

        SpecDocument document = await ReadAsync(path, fromCorpus: false);

        List<Diagnostic> found = [];
        new ReferenceGraph().Resolve(document, ReferencePolicy.Closed, LoadLimits.Default, found);

        found.Should().Contain(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.DisallowedReference, StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_allowed_external_reference_is_preserved_exactly_as_written()
    {
        string path = Write(
            "externe-permise.yaml",
            """
            openapi: 3.0.3
            info:
              title: Référence externe permise
              version: 1.0.0
            paths:
              /documents:
                get:
                  operationId: listerDocuments
                  responses:
                    '200':
                      description: Les documents.
                      content:
                        application/json:
                          schema:
                            $ref: 'commun.yaml#/components/schemas/Document'
            """);

        SpecDocument document = await ReadAsync(path, fromCorpus: false);

        List<Diagnostic> found = [];
        ReferenceGraphResult graph = new ReferenceGraph().Resolve(
            document, new ReferencePolicy(AllowLocal: true), LoadLimits.Default, found);

        found.Should().BeEmpty();
        graph.ExternalPreserved.Should().ContainSingle();
        graph.ExternalPreserved.Single().To.ToString().Should().Be(
            "commun.yaml#/components/schemas/Document");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Write(string fileName, string content)
    {
        string path = Path.Combine(_root, fileName);
        File.WriteAllText(path, content);

        return path;
    }

    private async Task<SpecDocument> ReadAsync(string pathOrName, bool fromCorpus = true)
    {
        string path = fromCorpus ? Write(pathOrName, CorpusLoader.Read(pathOrName)) : pathOrName;

        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }
}
