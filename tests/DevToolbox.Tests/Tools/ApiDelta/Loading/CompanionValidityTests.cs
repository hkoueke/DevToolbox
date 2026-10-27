using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Loading;

/// <summary>
/// Un fichier compagnon imparfait arrête-t-il une consolidation qu'il n'empêche pas ?
/// </summary>
/// <remarks>
/// Il ne doit pas. On ne répond que de ce qu'on écrit : la production est recopiée puis republiée, donc
/// elle doit tenir ; le compagnon n'est jamais publié, et le document consolidé répond de lui-même par sa
/// propre vérification.
/// <para>
/// Les refuser ensemble rendait l'outil inutilisable sur les fichiers qui existent vraiment. Un export
/// Swagger dont « info » est vide est le cas ordinaire — celui qui a été soumis en essai réel — et aucune
/// comparaison de routes ne dépend de « info.title ». Le refus n'était pas prudent, il était hors sujet.
/// </para>
/// </remarks>
public sealed class CompanionValidityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_companion_with_an_empty_info_block_does_not_stop_the_run()
    {
        ToolRunContext context = Context(Companion("{\"openapi\":\"3.0.1\",\"info\":{},\"paths\":{}}"));

        Result validated = await Validate(context).ConfigureAwait(true);

        validated.IsSuccess.Should().BeTrue(
            "le compagnon n'est jamais publié : son « info » vide ne peut pas arrêter l'exécution — "
            + validated.Failure?.Message);

        // Et la plainte n'est ni perdue ni muette : elle est consignée, et elle nomme le compagnon.
        IReadOnlyList<Diagnostic> said = DiagnosticCollector.Of(context);

        said.Should().Contain(
            diagnostic => diagnostic.IsBlocking
                && diagnostic.Code == DiagnosticCodes.StructuralViolation,
            "ce qui est refusé reste dit");

        said.Where(diagnostic => string.Equals(diagnostic.Code, DiagnosticCodes.StructuralViolation, StringComparison.Ordinal))
            .Should().OnlyContain(
                diagnostic => string.Equals(diagnostic.Document, Compagnon, StringComparison.Ordinal),
                "chaque verdict nomme le fichier qu'il regarde, et non celui qu'on affiche en premier");
    }

    [Fact]
    public async Task The_same_fault_in_the_production_file_does_stop_the_run()
    {
        // Le revers, et c'est lui qui rend la permission acceptable : ce qu'on va republier doit tenir.
        ToolRunContext context = Context(
            Companion(CorpusLoader.Read("candidate-additive.yaml")),
            production: "openapi: 3.0.3\ninfo: {}\npaths: {}\n");

        Result validated = await Validate(context).ConfigureAwait(true);

        validated.IsFailure.Should().BeTrue("la production est recopiée puis republiée : elle doit tenir");
        validated.Failure!.Message.Should().Contain("votre fichier de production");
    }

    [Fact]
    public void An_imperfect_companion_does_not_make_a_published_run_report_a_load_failure()
    {
        // Ce que les journaux d'un essai réel montraient : « Issue : loadFailure » sur une exécution qui
        // venait d'écrire un document vérifié. Un code d'issue qui dit l'échec d'une réussie apprend à
        // ne plus lire les codes d'issue.
        ApiDeltaRun run = Run(
            new Diagnostic(
                DiagnosticCodes.StructuralViolation,
                DiagnosticSeverity.Error,
                "The field 'title' in 'info' object is REQUIRED.",
                Document: Compagnon));

        OutcomeResolver.Resolve(run, FailureThreshold.Never).Should().Be(OutcomeCode.Clean);
    }

    [Fact]
    public void The_same_verdict_on_the_production_is_a_load_failure()
    {
        ApiDeltaRun run = Run(
            new Diagnostic(
                DiagnosticCodes.StructuralViolation,
                DiagnosticSeverity.Error,
                "The field 'title' in 'info' object is REQUIRED.",
                Document: Production));

        OutcomeResolver.Resolve(run, FailureThreshold.Never).Should().Be(OutcomeCode.LoadFailure);
    }

    [Fact]
    public void A_companion_that_could_not_be_parsed_at_all_is_still_a_load_failure()
    {
        // La structure seulement. Un compagnon illisible n'a rien laissé à comparer, et c'est bien un
        // échec de lecture : la permission ci-dessus ne doit pas s'étendre jusque-là.
        ApiDeltaRun run = Run(
            new Diagnostic(
                DiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "Ce document n'est pas analysable.",
                Document: Compagnon));

        OutcomeResolver.Resolve(run, FailureThreshold.Never).Should().Be(OutcomeCode.LoadFailure);
    }

    private const string Production = "production.yaml";
    private const string Compagnon = "compagnon.json";

    private static Task<Result> Validate(ToolRunContext context) =>
        new ValidateStep(new OpenApiSemanticAdapter(), new ReferenceGraph())
            .ExecuteAsync(context, CancellationToken.None);

    private static SpecDocument Companion(string content) =>
        Read(content, Compagnon, DocumentRole.Candidate);

    private static ToolRunContext Context(
        SpecDocument companion, string? production = null)
    {
        ToolRunContext context = new(
            new ToolRun(Guid.NewGuid(), "apidelta", Now, [ApiDeltaStepNames.Validate]));

        context.Set(
            ApiDeltaContextKeys.Selection,
            new RunSelection(RunMode.Consolidate, Production, string.Empty, "artefacts"));

        context.Set(
            ApiDeltaContextKeys.ProductionDocument,
            Read(
                production ?? CorpusLoader.Read("baseline-30.yaml"),
                Production,
                DocumentRole.Production));

        context.Set(ApiDeltaContextKeys.CandidateDocument, companion);

        return context;
    }

    private static SpecDocument Read(string content, string name, DocumentRole role)
    {
        SpecReadResult result = new SpecReader(NullLogger<SpecReader>.Instance).ReadBytes(
            Encoding.UTF8.GetBytes(content), name, role, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            name + " doit se lire : "
            + string.Join(" / ", result.Diagnostics.Select(one => one.Message)));

        return result.Document;
    }

    /// <summary>Une exécution qui a publié, portant le diagnostic qu'on veut peser.</summary>
    private static ApiDeltaRun Run(Diagnostic said) => new(
        Guid.Empty,
        new RunSelection(RunMode.Consolidate, Production, string.Empty, "artefacts"),
        DateTimeOffset.UnixEpoch,
        TimeSpan.Zero,
        Read(CorpusLoader.Read("baseline-30.yaml"), Production, DocumentRole.Production),
        Read(CorpusLoader.Read("candidate-additive.yaml"), Compagnon, DocumentRole.Candidate),
        [said],
        [],
        []);
}
