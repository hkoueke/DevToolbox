namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce qu'une règle rapporte d'elle-même, qu'elle ait trouvé quelque chose ou non.</summary>
/// <param name="RuleId">L'identifiant de la règle.</param>
/// <param name="State">Ce qu'il en est advenu.</param>
/// <param name="FindingCount">Le nombre de constats rendus.</param>
/// <param name="Reason">Pourquoi, quand l'état demande une explication.</param>
public sealed record RuleOutcome(
    string RuleId,
    RuleState State,
    int FindingCount,
    string? Reason = null);
