using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevToolbox.Tests.Tools.ApiDelta.Optimization;

/// <summary>
/// Monte l'optimiseur comme la racine de composition le monte : les huit règles, l'émetteur canonique et
/// le vérificateur d'équivalence.
/// </summary>
internal static class OptimizationHarness
{
    /// <summary>Les huit règles d'optimisation.</summary>
    internal static IReadOnlyList<IOptimizationRule> Rules { get; } =
    [
        new ExtractParametersRule(),
        new ExtractResponsesRule(),
        new ExtractRequestBodiesRule(),
        new ExtractHeadersAndExamplesRule(),
        new ExtractInlineSchemasRule(),
        new RemoveUnusedComponentsRule(),
        new HoistPathParametersRule(),
        new AddDeprecationFlagRule(),
    ];

    /// <summary>La règle d'un identifiant.</summary>
    internal static IOptimizationRule Rule(string ruleId) =>
        Rules.Single(rule => string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal));

    /// <summary>Lit une description écrite pour l'essai.</summary>
    internal static async Task<SpecDocument> WrittenAsync(string root, string content)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".yaml");
        await File.WriteAllTextAsync(path, content);

        return await ReadAsync(path);
    }

    /// <summary>Lit un fichier du corpus.</summary>
    internal static async Task<SpecDocument> CorpusAsync(string root, string fileName)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + "-" + fileName);
        await File.WriteAllTextAsync(path, CorpusLoader.Read(fileName));

        return await ReadAsync(path);
    }

    /// <summary>Monte l'applicateur, avec des règles de remplacement s'il en faut.</summary>
    internal static OptimizationApplier Applier(IReadOnlyList<IOptimizationRule>? rules = null) =>
        new(rules ?? Rules, new CanonicalEmitter(), Verifier());

    /// <summary>Monte le vérificateur d'équivalence.</summary>
    internal static ContractEquivalenceVerifier Verifier()
    {
        OpenApiMetamodel metamodel = new();

        return new ContractEquivalenceVerifier(
            new SpecReader(NullLogger<SpecReader>.Instance),
            new OpenApiSemanticAdapter(),
            new ReferenceGraph(),
            new Fingerprinter(new Canonicalizer(metamodel)));
    }

    /// <summary>Le plan d'un document, examiné puis chiffré.</summary>
    internal static OptimizationPlan Plan(SpecDocument document, Ruleset? ruleset = null) =>
        TransformationPlanner.Plan(
            document,
            Audit.AuditHarness.Audit(document, ruleset),
            ruleset ?? Ruleset.Default);

    /// <summary>La transformation d'une règle dans un plan.</summary>
    internal static Transformation Of(OptimizationPlan plan, string ruleId) =>
        plan.Transformations.First(transformation =>
            string.Equals(transformation.RuleId, ruleId, StringComparison.Ordinal));

    /// <summary>Réécrit un arbre, pour lire ce qu'une édition a produit.</summary>
    internal static string Text(SpecNode root) =>
        System.Text.Encoding.UTF8.GetString(
            new CanonicalEmitter().Emit(root, CanonicalFormatting.Default).Span);

    private static async Task<SpecDocument> ReadAsync(string path)
    {
        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }
}
