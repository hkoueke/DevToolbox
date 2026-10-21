using DevToolbox.Application.Abstractions;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Monte l'étape d'écriture comme la racine de composition la monte.
/// </summary>
/// <remarks>
/// En un seul endroit : l'étape porte huit collaborateurs, et chaque harnais qui les énumérerait pour lui
/// seul deviendrait une version divergente de la composition réelle. Les règles d'optimisation viennent
/// d'une liste écrite ici, sauf que l'essai de la racine de composition vérifie séparément que
/// l'enregistrement réel les porte toutes.
/// </remarks>
internal static class WriteStepFactory
{
    /// <summary>Les règles d'optimisation, dans l'ordre de leur identifiant.</summary>
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

    /// <summary>Monte l'étape de sélection, configuration comprise.</summary>
    /// <param name="prompt">Les invites, déjà programmées par l'essai.</param>
    /// <returns>L'étape.</returns>
    internal static SelectStep Select(IApiDeltaPrompt prompt) =>
        new(
            prompt,
            new SpecFileProbe(NullLogger<SpecFileProbe>.Instance),
            new RulesetStore(),
            new SuppressionStore(),
            ApiDeltaOptions.Default);

    /// <summary>Monte l'étape d'écriture.</summary>
    /// <param name="prompt">Les invites, déjà programmées par l'essai.</param>
    /// <param name="clock">L'horloge figée de l'essai.</param>
    /// <param name="rules">
    /// Les règles d'optimisation, quand l'essai en substitue une pour éprouver un refus.
    /// </param>
    /// <returns>L'étape.</returns>
    internal static WriteStep Create(
        IApiDeltaPrompt prompt,
        IClock clock,
        IReadOnlyList<IOptimizationRule>? rules = null)
    {
        OpenApiMetamodel metamodel = new();
        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        return new WriteStep(
            new ArtifactStore(prompt, NullLogger<ArtifactStore>.Instance),
            new OptimizationApplier(
                rules ?? Rules,
                new CanonicalEmitter(),
                new ContractEquivalenceVerifier(
                    reader,
                    new OpenApiSemanticAdapter(),
                    new ReferenceGraph(),
                    new Fingerprinter(new Canonicalizer(metamodel)))),
            new InPlaceWriter(prompt, NullLogger<InPlaceWriter>.Instance),
            ApiDeltaOptions.Default,
            clock);
    }
}
