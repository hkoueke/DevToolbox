using System.Text;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Les trois étapes qui amènent un contrat de service jusqu'à la comparaison se passent-elles le relais ?
/// </summary>
/// <remarks>
/// Chacune était éprouvée seule, et aucune épreuve ne les faisait se suivre : deux d'entre elles sont
/// restées hors de la chaîne pendant deux tranches sans qu'aucun essai ne s'en aperçoive. L'outil ne
/// savait pas lire un contrat de service alors que tout ce qu'il faut pour le lire existait et passait.
/// <para>
/// C'est le relais qui est éprouvé ici, et non ce que chaque étape fait : la lecture dépose un contrat,
/// la lecture OpenAPI s'efface devant lui au lieu de buter sur sa première balise, et l'appariement
/// dépose le document projeté sous la clé que la comparaison lira.
/// </para>
/// </remarks>
public sealed class WsdlPipelineTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-fil-wsdl-" + Guid.NewGuid().ToString("N"));

    public WsdlPipelineTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_wsdl_companion_reaches_the_comparison_as_a_projected_document()
    {
        ToolRunContext context = await ContextAsync().ConfigureAwait(true);

        // 1. La lecture du contrat reconnaît un WSDL à son contenu et dépose le contrat.
        Result read = await new ReadWsdlStep(new WsdlReader(), Fetcher())
            .ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);

        read.IsSuccess.Should().BeTrue(read.Failure?.Message);

        WsdlContract? contract = context.Get<WsdlContract>(ApiDeltaContextKeys.WsdlContract);

        contract.Should().NotBeNull("l'étape doit avoir reconnu et lu le contrat");
        contract.Operations.Should().HaveCount(5);

        // 2. La lecture OpenAPI s'efface : lire ce fichier comme une description échouerait.
        Result source = await new ReadSourceStep(
                new SpecReader(NullLogger<SpecReader>.Instance))
            .ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);

        source.IsSuccess.Should().BeTrue(
            "le compagnon est un contrat : cette étape n'a rien à faire, et surtout pas à échouer — "
            + source.Failure?.Message);

        // 3. L'appariement dépose le document projeté là où la comparaison le lira.
        Result paired = await new CorrespondStep(
                new Projector(), new SimilarityProposer(), Confirming())
            .ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);

        paired.IsSuccess.Should().BeTrue(paired.Failure?.Message);

        SpecDocument? candidate = context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument);

        candidate.Should().NotBeNull(
            "sans cela, la comparaison n'a pas de candidat et tout ce qui suit est sans objet");

        candidate.Format.Should().Be(SpecFormat.Projected);
        candidate.Should().BeSameAs(
            context.Get<ProjectionResult>(ApiDeltaContextKeys.WsdlProjection)!.Document);

        context.Get<WsdlFooting>(ApiDeltaContextKeys.WsdlFooting).Should().NotBeNull(
            "la base de la sélection voyage avec le reste");
    }

    [Fact]
    public async Task An_openapi_companion_leaves_the_contract_steps_with_nothing_to_do()
    {
        // Le revers : les deux étapes du contrat sont dans la chaîne pour tout le monde, et ne doivent
        // rien faire quand le compagnon est une description OpenAPI ordinaire.
        ToolRunContext context = await ContextAsync("candidate-additive.yaml").ConfigureAwait(true);

        Result read = await new ReadWsdlStep(new WsdlReader(), Fetcher())
            .ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);

        read.IsSuccess.Should().BeTrue(read.Failure?.Message);
        context.Get<WsdlContract>(ApiDeltaContextKeys.WsdlContract).Should().BeNull();

        Result paired = await new CorrespondStep(
                new Projector(), new SimilarityProposer(), Confirming())
            .ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);

        paired.IsSuccess.Should().BeTrue(paired.Failure?.Message);
        context.Get<WsdlFooting>(ApiDeltaContextKeys.WsdlFooting).Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Un contexte portant la production lue et un compagnon sur le disque.</summary>
    private async Task<ToolRunContext> ContextAsync(string companion = "catalogue.wsdl")
    {
        foreach (string name in new[] { "catalogue.wsdl", "catalogue-types.xsd", "commun.xsd" })
        {
            await File.WriteAllTextAsync(
                Path.Combine(_root, name),
                CorpusLoader.Read("Wsdl." + name),
                new UTF8Encoding(false)).ConfigureAwait(true);
        }

        if (!companion.EndsWith(".wsdl", StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(
                Path.Combine(_root, companion),
                CorpusLoader.Read(companion),
                new UTF8Encoding(false)).ConfigureAwait(true);
        }

        ToolRunContext context = new(
            new ToolRun(
                Guid.NewGuid(),
                "apidelta",
                Now,
                [ApiDeltaStepNames.ReadWsdl, ApiDeltaStepNames.ReadSource, ApiDeltaStepNames.Correspond]));

        context.Set(
            ApiDeltaContextKeys.Selection,
            new RunSelection(
                RunMode.Consolidate,
                Path.Combine(_root, "production.yaml"),
                string.Empty,
                _root,
                CandidatePath: Path.Combine(_root, companion)));

        SpecReadResult production = new SpecReader(NullLogger<SpecReader>.Instance).ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read("Wsdl.production.yaml")),
            "production.yaml",
            DocumentRole.Production,
            LoadLimits.Default);

        production.Document.Should().NotBeNull();
        context.Set(ApiDeltaContextKeys.ProductionDocument, production.Document);

        return context;
    }

    private static IContractFetcher Fetcher() => Substitute.For<IContractFetcher>();

    private static ICorrespondencePrompt Confirming()
    {
        ICorrespondencePrompt prompt = Substitute.For<ICorrespondencePrompt>();

        prompt.ConfirmAsync(
                Arg.Any<OperationCorrespondence>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<OperationCorrespondence>().Confirm()));

        return prompt;
    }
}
