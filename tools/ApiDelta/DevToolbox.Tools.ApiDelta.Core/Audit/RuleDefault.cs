namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce qu'une règle vaut par défaut.</summary>
/// <param name="RuleId">L'identifiant de la règle.</param>
/// <param name="Severity">La gravité à défaut de décision contraire.</param>
/// <param name="Safety">
/// La meilleure classe de sûreté que le remède puisse atteindre. Une règle peut la dégrader au vu du
/// document — jamais l'améliorer : aucun réglage ne rend sûr ce qui n'est pas prouvé.
/// </param>
public sealed record RuleDefault(string RuleId, FindingSeverity Severity, SafetyClass Safety);
