using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Extraction;

/// <summary>
/// Ce que l'extraction refuse de produire, et ce qu'elle dit en refusant.
/// </summary>
public sealed class ExtractRefusalTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-extrait-refus-" + Guid.NewGuid().ToString("N"));

    public ExtractRefusalTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task An_empty_selection_is_refused_before_any_work()
    {
        // Il n'existe pas d'extrait vide : un fichier OpenAPI sans chemin ne décrit rien, et le produire
        // sans se plaindre laisserait croire que la sélection avait abouti.
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        Result<BuiltExtract> built = ExtractHarness.Builder()
            .Build(source, new ExtractRequest([]), CancellationToken.None);

        built.IsFailure.Should().BeTrue();
        built.Failure!.Message.Should().Contain("il n'existe pas d'extrait vide");
    }

    [Fact]
    public async Task A_selected_operation_the_description_no_longer_has_is_refused_by_name()
    {
        // La reprise relit, et une opération disparue depuis doit être nommée : c'est la différence
        // entre « relancez la sélection » et « relancez la sélection, GET /disparu n'existe plus ».
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        Result<BuiltExtract> built = ExtractHarness.Builder().Build(
            source,
            new ExtractRequest([new OperationSelector("/disparu", HttpOperationMethod.Get)]),
            CancellationToken.None);

        built.IsFailure.Should().BeTrue();
        built.Failure!.Message.Should().Contain("GET /disparu")
            .And.Contain("ne sont plus dans la description");
    }

    [Fact]
    public async Task A_builder_that_drops_a_path_level_parameter_fails_verification_and_names_it()
    {
        // Le défaut que ce mode doit être incapable de produire : un extrait qui se relit, qui se valide,
        // et qui a perdu le paramètre que son chemin imposait. Un constructeur délibérément abîmé est la
        // seule façon d'établir que la vérification l'attrape.
        SpecDocument source = await ExtractHarness.CorpusAsync(_root, "baseline-30.yaml");

        OperationSelector selector = new("/documents", HttpOperationMethod.Get);

        ExtractResult result = ExtractHarness
            .Extract(source, [selector], new ForgetfulBuilder(ExtractHarness.Builder())).Value;

        result.Verification.Established.Should().BeFalse();
        result.Verification.OperationsEquivalent.Should().BeFalse();
        result.Publishable.Should().BeFalse();
        result.Serialized.Length.Should().Be(0, "rien ne doit être disponible à l'écriture");

        result.Verification.Explanation.Should().Contain("GET /documents")
            .And.Contain("parameters");
    }

    [Fact]
    public async Task An_extract_carrying_an_unselected_operation_fails_verification()
    {
        // La façon la plus naturelle de se tromper : emporter l'opération voisine « parce qu'elle était
        // dans le même élément de chemin ». Et la plus difficile à remarquer en relisant le fichier.
        // Le document est écrit sans composant partagé, pour que la seule épreuve qui puisse échouer
        // soit celle-là : avec des références à combler, c'est la résolution qui refuserait d'abord, et
        // l'essai ne dirait plus rien de l'épreuve qu'il vise.
        SpecDocument source = await ExtractHarness.WrittenAsync(_root, Neighbours);

        ExtractResult result = ExtractHarness.Extract(
            source,
            [new OperationSelector("/un", HttpOperationMethod.Get)],
            new GenerousBuilder(ExtractHarness.Builder())).Value;

        result.Verification.ContainsNothingUnselected.Should().BeFalse();
        result.Verification.Established.Should().BeFalse();
        result.Verification.Differences.Should().Contain(difference =>
            difference.Contains("POST /un", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_operation_whose_only_dependency_is_external_is_refused_while_remote_is_off()
    {
        // La politique par défaut est fermée. Un extrait dont une référence sort du document ne se
        // résout pas, et il ne doit pas sortir en prétendant être autonome.
        SpecDocument source = await ExtractHarness.WrittenAsync(_root, External);

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/un", HttpOperationMethod.Get)]).Value;

        result.Verification.ReferencesResolved.Should().BeFalse();
        result.Verification.Established.Should().BeFalse();
        result.Publishable.Should().BeFalse();
    }

    [Fact]
    public async Task A_carried_extension_naming_an_absent_path_is_reported_and_never_edited()
    {
        // L'outil ne connaît pas la grammaire de cette extension. La corriger serait réécrire une donnée
        // dont il ignore le sens ; la taire laisserait une configuration de passerelle désignant le vide.
        SpecDocument source = await ExtractHarness.WrittenAsync(_root, Gateway);

        ExtractResult result = ExtractHarness
            .Extract(source, [new OperationSelector("/un", HttpOperationMethod.Get)]).Value;

        result.Verification.Established.Should().BeTrue(result.Verification.Explanation);

        result.DanglingExtensionReferences.Should().ContainSingle()
            .Which.Message.Should().Contain("x-sg-passerelle").And.Contain("/deux");

        // Et l'extension est tout de même là, intacte.
        ExtractHarness.Text(result).Should().Contain("/deux");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Un constructeur qui oublie les paramètres du niveau du chemin. Il n'existe que pour éprouver que
    /// la vérification le remarque.
    /// </summary>
    private sealed class ForgetfulBuilder(IExtractBuilder inner) : IExtractBuilder
    {
        public Result<BuiltExtract> Build(
            SpecDocument source,
            ExtractRequest request,
            CancellationToken cancellationToken)
        {
            Result<BuiltExtract> built = inner.Build(source, request, cancellationToken);

            if (built.IsFailure)
            {
                return built;
            }

            SpecNode abimed = built.Value.Document;

            foreach (OperationSelector selector in request.Selectors)
            {
                abimed = TreeEdits.Remove(
                    abimed,
                    SpecPointer.Root.Child("paths").Child(selector.Path).Child("parameters"));
            }

            return Result.Success(built.Value with { Document = abimed });
        }
    }

    /// <summary>Un constructeur qui emporte les opérations voisines du chemin retenu.</summary>
    private sealed class GenerousBuilder(IExtractBuilder inner) : IExtractBuilder
    {
        public Result<BuiltExtract> Build(
            SpecDocument source,
            ExtractRequest request,
            CancellationToken cancellationToken)
        {
            Result<BuiltExtract> built = inner.Build(source, request, cancellationToken);

            if (built.IsFailure)
            {
                return built;
            }

            SpecNode generous = built.Value.Document;

            foreach (OperationSelector selector in request.Selectors)
            {
                SpecPointer path = SpecPointer.Root.Child("paths").Child(selector.Path);

                if (PointerNavigator.Find(source.Root, path.Json) is MappingNode item
                    && item.Find("post") is { } neighbour)
                {
                    generous = TreeEdits.Insert(generous, path.Child("post"), neighbour);
                }
            }

            return Result.Success(built.Value with { Document = generous });
        }
    }

    private const string Neighbours = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            get:
              operationId: lireUn
              responses:
                '200':
                  description: Bon.
            post:
              operationId: ecrireUn
              responses:
                '201':
                  description: Créé.
        """;

    private const string External = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            get:
              operationId: lireUn
              responses:
                '200':
                  description: Bon.
                  content:
                    application/json:
                      schema:
                        $ref: 'autre.yaml#/components/schemas/Ailleurs'
        """;

    private const string Gateway = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        x-sg-passerelle:
          routes:
            - /un
            - /deux
        paths:
          /un:
            get:
              operationId: lireUn
              responses:
                '200':
                  description: Bon.
          /deux:
            get:
              operationId: lireDeux
              responses:
                '200':
                  description: Bon.
        """;
}
