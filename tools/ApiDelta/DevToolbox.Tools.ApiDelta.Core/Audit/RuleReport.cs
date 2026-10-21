namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce qu'une règle rend : ses constats, et l'état dans lequel elle les rend.</summary>
/// <param name="State">Ce qu'il en est advenu.</param>
/// <param name="Findings">Les constats.</param>
/// <param name="Reason">Pourquoi, quand l'état demande une explication.</param>
public sealed record RuleReport(
    RuleState State,
    IReadOnlyList<Finding> Findings,
    string? Reason = null)
{
    /// <summary>Un rapport sans constat, d'une règle qui a bien tourné.</summary>
    public static RuleReport Clean { get; } = new(RuleState.Evaluated, []);

    /// <summary>Une règle qui a tourné sur tout ce qu'elle devait voir.</summary>
    /// <param name="findings">Ce qu'elle a trouvé.</param>
    /// <returns>Le rapport.</returns>
    public static RuleReport Evaluated(IReadOnlyList<Finding> findings) =>
        new(RuleState.Evaluated, findings);

    /// <summary>Une règle qui n'a pas pu tourner.</summary>
    /// <param name="reason">Ce qui l'en a empêchée, dit à un lecteur.</param>
    /// <returns>Le rapport.</returns>
    /// <remarks>
    /// Sans constat, et c'est le point : zéro constat et « pas évaluée » ne sont pas la même nouvelle.
    /// </remarks>
    public static RuleReport NotEvaluated(string reason) =>
        new(RuleState.NotEvaluated, [], reason);

    /// <summary>Une règle qui a rendu moins que ce qu'elle a trouvé.</summary>
    /// <param name="findings">Ce qu'elle rend.</param>
    /// <param name="reason">Ce qui a été laissé de côté.</param>
    /// <returns>Le rapport.</returns>
    public static RuleReport Truncated(IReadOnlyList<Finding> findings, string reason) =>
        new(RuleState.Truncated, findings, reason);
}
