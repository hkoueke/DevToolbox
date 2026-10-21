using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Optimization;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Applique à une copie ce qu'un plan a proposé, puis le vérifie.</summary>
public interface IOptimizationApplier
{
    /// <summary>Applique le plan.</summary>
    /// <param name="plan">Ce qui a été proposé et chiffré.</param>
    /// <param name="production">La description d'origine, qui n'est pas modifiée.</param>
    /// <param name="selection">Ce que le développeur a autorisé.</param>
    /// <param name="ruleset">Ce que l'équipe a décidé.</param>
    /// <returns>Ce qui a été appliqué, ce qui a été écarté, et ce que la vérification a établi.</returns>
    OptimizationOutcome Apply(
        OptimizationPlan plan,
        SpecDocument production,
        RunSelection selection,
        Ruleset ruleset);
}
