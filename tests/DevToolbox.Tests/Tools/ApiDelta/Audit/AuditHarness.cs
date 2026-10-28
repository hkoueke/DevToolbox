using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using DevToolbox.Tools.ApiDelta.Presentation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevToolbox.Tests.Tools.ApiDelta.Audit;

/// <summary>
/// Monte l'examen tel que l'exécution le monte : les règles câblées, le graphe des références résolu et
/// les verdicts de structure.
/// </summary>
/// <remarks>
/// Les règles viennent de l'enregistrement réel et non d'une liste écrite ici : une règle oubliée dans le
/// câblage doit faire échouer un essai, et un harnais qui tiendrait sa propre liste ne le verrait pas.
/// </remarks>
internal static class AuditHarness
{
    /// <summary>Les règles telles que la racine de composition les enregistre.</summary>
    internal static IReadOnlyList<IAuditRule> Wired()
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddSingleton(ApiDeltaOptions.Default);
        services.AddApiDeltaInfrastructure();
        services.AddApiDelta();

        using ServiceProvider provider = services.BuildServiceProvider();

        return [.. provider.GetServices<IAuditRule>()];
    }

    /// <summary>Lit un document du corpus, fidèlement.</summary>
    internal static async Task<SpecDocument> ReadAsync(string root, string fileName)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + "-" + fileName);
        await File.WriteAllTextAsync(path, CorpusLoader.Read(fileName));

        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }

    /// <summary>Examine un document du corpus, avec tout ce que l'exécution donnerait aux règles.</summary>
    internal static async Task<AuditResult> AuditAsync(
        string root,
        string fileName,
        Ruleset? ruleset = null)
    {
        SpecDocument document = await ReadAsync(root, fileName);

        return Audit(document, ruleset);
    }

    /// <summary>Examine un document déjà lu.</summary>
    internal static AuditResult Audit(SpecDocument document, Ruleset? ruleset = null)
    {
        OpenApiMetamodel metamodel = new();
        Fingerprinter fingerprinter = new(new Canonicalizer(metamodel));

        List<Diagnostic> resolution = [];
        ReferenceGraphResult references = new ReferenceGraph()
            .Resolve(document, ReferencePolicy.Closed, LoadLimits.Default, resolution);

        return new AuditEngine(Wired(), metamodel, fingerprinter)
            .Run(
                new AuditRequest(
                    document,
                    ruleset ?? Ruleset.Default,
                    references,
                    new OpenApiSemanticAdapter().Validate(document)),
                CancellationToken.None);
    }

    /// <summary>Les constats d'une règle, dans l'ordre du rapport.</summary>
    internal static IReadOnlyList<Finding> Of(AuditResult audit, string ruleId) =>
    [
        .. audit.Findings.Where(finding =>
            string.Equals(finding.RuleId, ruleId, StringComparison.Ordinal)),
    ];

    /// <summary>Le sort d'une règle.</summary>
    internal static RuleOutcome Outcome(AuditResult audit, string ruleId) =>
        audit.Outcomes.Single(outcome =>
            string.Equals(outcome.RuleId, ruleId, StringComparison.Ordinal));
}
