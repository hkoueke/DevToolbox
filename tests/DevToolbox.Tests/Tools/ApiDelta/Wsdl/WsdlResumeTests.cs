using System.Text;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Presentation;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Une séance interrompue après l'appariement redemande-t-elle de l'apparier ?
/// </summary>
/// <remarks>
/// Elle ne doit pas. Apparier deux cents opérations à la main est le travail le plus long de la séance,
/// et c'est au moment de reprendre qu'un développeur a le moins envie de le refaire. Mémoriser
/// l'appariement n'a d'intérêt que si la reprise le consomme — et c'est ce que cette épreuve tient,
/// parce que la mémorisation existait avant la consommation.
/// <para>
/// Ce qui ne survit pas, en revanche, est tout le reste : reprendre relit. La sélection seule rejoint le
/// point de reprise, et aucun contenu de contrat n'y entre jamais.
/// </para>
/// </remarks>
public sealed class WsdlResumeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_resumed_run_does_not_ask_for_the_pairing_again()
    {
        ICorrespondencePrompt prompt = Asking();
        ToolRunContext context = Context();

        context.Set(
            ApiDeltaContextKeys.Selection,
            Selection() with { Correspondence = Paired().Confirm() });

        Result result = await Run(context, prompt).ConfigureAwait(true);

        result.IsSuccess.Should().BeTrue();

        await prompt.DidNotReceive().ConfirmAsync(
            Arg.Any<OperationCorrespondence>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);

        OperationCorrespondence settled =
            context.Get<OperationCorrespondence>(ApiDeltaContextKeys.Correspondence)!;

        settled.IsConfirmed.Should().BeTrue();
        settled.For("deposerDocument")!.Route.Should().Be("POST /documents");

        context.Get<CoverageReport>(ApiDeltaContextKeys.Coverage)!.IsEstablished.Should().BeTrue(
            "la couverture se rétablit depuis l'appariement repris, sans rien redemander");
    }

    [Fact]
    public async Task A_pairing_that_was_never_confirmed_is_asked_again()
    {
        // Le revers : une proposition n'est pas une décision. La restituer à la place du développeur
        // reviendrait à confirmer pour lui ce qu'il n'avait pas confirmé.
        ICorrespondencePrompt prompt = Asking();
        ToolRunContext context = Context();

        context.Set(ApiDeltaContextKeys.Selection, Selection() with { Correspondence = Paired() });

        Result result = await Run(context, prompt).ConfigureAwait(true);

        result.IsSuccess.Should().BeTrue();

        await prompt.Received(1).ConfirmAsync(
            Arg.Any<OperationCorrespondence>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public void A_confirmed_pairing_survives_the_checkpoint_and_carries_no_contract_content()
    {
        ToolRunContext context = Context();

        context.Set(
            ApiDeltaContextKeys.Selection,
            Selection() with { Correspondence = Paired().Confirm() });

        IClock clock = Substitute.For<IClock>();

        clock.UtcNow.Returns(Now);

        PersistedRun checkpoint = new ApiDeltaCheckpointFactory(clock).TryCreate(context)!;
        RunSelection? read = ApiDeltaCheckpointFactory.TryRead(checkpoint);

        read.Should().NotBeNull();
        read.Correspondence.Should().NotBeNull();
        read.Correspondence.IsConfirmed.Should().BeTrue("seul un appariement confirmé est mémorisé");
        read.Correspondence.For("deposerDocument")!.Route.Should().Be("POST /documents");

        // Et rien du contrat : le point de reprise ne porte que la sélection, et un appariement est une
        // sélection — des chaînes et des énumérations, aucun chemin vers un nœud ni vers un type.
        string written = System.Text.Json.JsonSerializer.Serialize(checkpoint);

        written.Should().NotContain("complexType");
        written.Should().NotContain("xs:");
    }

    private static Task<Result> Run(ToolRunContext context, ICorrespondencePrompt prompt) =>
        new CorrespondStep(new Projector(), new SimilarityProposer(), prompt)
            .ExecuteAsync(context, CancellationToken.None);

    /// <summary>Une invite qui confirmerait si on l'appelait — pour qu'un appel se voie.</summary>
    private static ICorrespondencePrompt Asking()
    {
        ICorrespondencePrompt prompt = Substitute.For<ICorrespondencePrompt>();

        prompt.ConfirmAsync(
                Arg.Any<OperationCorrespondence>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<OperationCorrespondence>().Confirm()));

        return prompt;
    }

    private static OperationCorrespondence Paired() => new(
        [new CorrespondenceEntry("deposerDocument", "POST /documents", CorrespondenceOrigin.Proposed)]);

    private static RunSelection Selection() => new(
        RunMode.Consolidate,
        "production.yaml",
        "sha256:3f9a7c21b0d4",
        Path.Combine("C:", "travail", "artefacts"),
        CandidatePath: "catalogue.wsdl",
        CandidateHash: "sha256:50997556b04c");

    private static ToolRunContext Context()
    {
        ToolRunContext context = new(
            new ToolRun(Guid.NewGuid(), "apidelta", Now, [ApiDeltaStepNames.Correspond]));

        context.Set(ApiDeltaContextKeys.WsdlContract, Contract());
        context.Set(ApiDeltaContextKeys.ProductionDocument, Production());

        return context;
    }

    /// <summary>Un contrat d'une opération, dont la route dérivée existe en production.</summary>
    private static WsdlContract Contract()
    {
        WsdlLocation location = new("catalogue.wsdl", "portType/operation", 1, 1);

        return new WsdlContract(
            [new WsdlDocument("catalogue.wsdl", WsdlDocumentKind.Definitions, 1, "x", "urn:a")],
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
            ".");
    }

    private static SpecDocument Production()
    {
        SpecReadResult result = new SpecReader(NullLogger<SpecReader>.Instance).ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read("Wsdl.production.yaml")),
            "production.yaml",
            DocumentRole.Production,
            LoadLimits.Default);

        result.Document.Should().NotBeNull();

        return result.Document;
    }
}
