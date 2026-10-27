using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Core;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Correspondence;

/// <summary>
/// L'étape compare-t-elle quelque chose avant que l'appariement soit confirmé ?
/// </summary>
/// <remarks>
/// La réponse doit être non, et c'est la règle la plus importante de cette tranche. L'étape est éprouvée
/// avec une invite substituée : on peut donc faire répondre « je renonce » et vérifier ce que l'étape
/// dépose, ce qu'aucun essai sur la console ne permettrait de faire proprement.
/// </remarks>
public sealed class CorrespondStepTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Declining_succeeds_and_establishes_nothing()
    {
        // Renoncer est une réponse. La séance réussit, n'ayant rien comparé, et la couverture n'est pas
        // établie — ni « tout couvert », ni « tout absent », qui seraient l'un rassurant et l'autre
        // alarmant, et tous deux faux.
        Harness harness = Harness.Declining();

        Result result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue("échouer ici punirait un développeur d'avoir lu");

        OperationCorrespondence settled = harness.Settled();

        settled.IsConfirmed.Should().BeFalse();
        settled.Comparable.Should().BeEmpty();
        harness.Coverage().IsEstablished.Should().BeFalse();
    }

    [Fact]
    public async Task Confirming_establishes_the_coverage_and_opens_the_comparison()
    {
        Harness harness = Harness.Confirming();

        Result result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();

        harness.Settled().IsConfirmed.Should().BeTrue();
        harness.Settled().Comparable.Should().NotBeEmpty();

        CoverageReport coverage = harness.Coverage();

        coverage.IsEstablished.Should().BeTrue();
        coverage.Entries.Should().HaveCount(1, "le contrat d'épreuve porte une opération");
    }

    [Fact]
    public async Task The_projection_is_deposited_whether_or_not_the_pairing_is_confirmed()
    {
        // La projection ne dépend pas de l'accord : elle décrit le contrat, pas la paire. Ses écarts
        // doivent donc atteindre le rapport même lorsque rien n'a été comparé, car ils disent ce que
        // l'outil n'aurait de toute façon pas su porter.
        Harness harness = Harness.Declining();

        _ = await harness.RunAsync();

        harness.Context.Get<ProjectionResult>(ApiDeltaContextKeys.WsdlProjection)
            .Should().NotBeNull();
    }

    [Fact]
    public async Task Nothing_happens_at_all_when_the_candidate_is_not_a_service_contract()
    {
        // Le candidat est une description OpenAPI : l'étape n'a rien à dire, et surtout ne doit pas
        // échouer. La séance de 003 doit traverser cette étape sans s'en apercevoir.
        Harness harness = Harness.WithoutContract();

        Result result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        harness.Context.Get<OperationCorrespondence>(ApiDeltaContextKeys.Correspondence)
            .Should().BeNull("rien n'a été apparié, et rien n'a été prétendu");
    }

    [Fact]
    public async Task A_run_without_a_production_document_fails_by_saying_what_is_missing()
    {
        Harness harness = Harness.WithoutProduction();

        Result result = await harness.RunAsync();

        result.IsSuccess.Should().BeFalse();
        result.Failure!.Message.Should().Contain("La production n'a pas été lue");
    }

    [Fact]
    public async Task The_prompt_is_shown_the_routes_production_actually_carries()
    {
        // Corriger une ligne demande de choisir parmi les routes existantes. Les lui cacher forcerait le
        // développeur à retaper une route, donc à se tromper.
        Harness harness = Harness.Confirming();

        _ = await harness.RunAsync();

        _ = harness.Prompt.Received(1).ConfirmAsync(
            Arg.Any<OperationCorrespondence>(),
            Arg.Is<IReadOnlyList<string>>(routes => routes.Contains("POST /documents")),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Le montage : un contrat d'une opération, une production d'une route.</summary>
    private sealed class Harness
    {
        private Harness(bool confirming, bool withContract = true, bool withProduction = true)
        {
            Context = new ToolRunContext(
                new ToolRun(Guid.NewGuid(), "apidelta", Now, [ApiDeltaStepNames.Correspond]));

            // Les clés sont posées ou absentes, jamais nulles : le contexte n'accepte pas de valeur
            // nulle, et « absente » est de toute façon l'état qu'une séance réelle présente.
            if (withContract)
            {
                Context.Set(ApiDeltaContextKeys.WsdlContract, Contract());
            }

            if (withProduction)
            {
                Context.Set(ApiDeltaContextKeys.ProductionDocument, Production());
            }

            Prompt = Substitute.For<ICorrespondencePrompt>();

            Prompt.ConfirmAsync(
                    Arg.Any<OperationCorrespondence>(),
                    Arg.Any<IReadOnlyList<string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(
                    confirming
                        ? call.Arg<OperationCorrespondence>().Confirm()
                        : call.Arg<OperationCorrespondence>()));
        }

        internal ToolRunContext Context { get; }

        internal ICorrespondencePrompt Prompt { get; }

        internal static Harness Declining() => new(confirming: false);

        internal static Harness Confirming() => new(confirming: true);

        internal static Harness WithoutContract() => new(confirming: true, withContract: false);

        internal static Harness WithoutProduction() => new(confirming: true, withProduction: false);

        internal Task<Result> RunAsync() =>
            new CorrespondStep(new Projector(), new SimilarityProposer(), Prompt)
                .ExecuteAsync(Context, CancellationToken.None);

        internal OperationCorrespondence Settled() =>
            Context.Get<OperationCorrespondence>(ApiDeltaContextKeys.Correspondence)!;

        internal CoverageReport Coverage() =>
            Context.Get<CoverageReport>(ApiDeltaContextKeys.Coverage)!;

        /// <summary>Un contrat d'une seule opération, dont la route dérivée existe en production.</summary>
        private static WsdlContract Contract()
        {
            WsdlLocation location = new("essai.wsdl", "portType/operation", 1, 1);

            return new WsdlContract(
                [new WsdlDocument("essai.wsdl", WsdlDocumentKind.Definitions, 1, "x", "urn:a")],
                [
                    new WsdlOperation(
                        "deposerDocument",
                        MessagePattern.RequestResponse,
                        null,
                        null,
                        [],
                        null,
                        null,
                        location),
                ],
                new Dictionary<string, XsdType>(StringComparer.Ordinal),
                [],
                [],
                Path.GetTempPath());
        }

        /// <summary>Une production portant « POST /documents », et rien d'autre.</summary>
        private static SpecDocument Production()
        {
            SpecPointer paths = SpecPointer.Root.Child("paths");
            SpecPointer route = paths.Child("/documents");

            MappingNode item = new(
                route,
                new SourcePosition(0, 0),
                ByteSpan.None,
                [
                    new MappingEntry(
                        DraftWriter.Text("post", route.Child("post")),
                        new MappingNode(
                            route.Child("post"), new SourcePosition(0, 0), ByteSpan.None, [])),
                ]);

            MappingNode root = new(
                SpecPointer.Root,
                new SourcePosition(0, 0),
                ByteSpan.None,
                [
                    new MappingEntry(
                        DraftWriter.Text("paths", paths),
                        new MappingNode(
                            paths,
                            new SourcePosition(0, 0),
                            ByteSpan.None,
                            [new MappingEntry(DraftWriter.Text("/documents", route), item)])),
                ]);

            return new SpecDocument(
                DocumentRole.Production,
                "production.yaml",
                SpecFormat.Yaml,
                OpenApiVersion.V30,
                0,
                string.Empty,
                root,
                ReadOnlyMemory<byte>.Empty);
        }
    }
}
