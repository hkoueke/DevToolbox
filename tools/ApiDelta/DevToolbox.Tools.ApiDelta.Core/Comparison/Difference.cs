using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>Un changement sémantique entre les deux descriptions.</summary>
/// <param name="ChangeId">L'identifiant stable de la sorte de changement.</param>
/// <param name="Location">Où il se trouve, pour une machine et pour un lecteur.</param>
/// <param name="Kind">Apparition, disparition ou modification.</param>
/// <param name="Before">Ce qu'il y avait, quand il y avait quelque chose.</param>
/// <param name="After">Ce qu'il y a, quand il y a quelque chose.</param>
/// <param name="Impact">Ce que cela fait aux appelants existants.</param>
/// <param name="Confidence">À quel point l'outil en répond.</param>
/// <param name="Rationale">Pourquoi il est classé ainsi.</param>
/// <param name="RecommendedAction">Ce qu'il y a à faire.</param>
/// <param name="Policy">La politique dont le classement dépend, le cas échéant.</param>
public sealed record Difference(
    string ChangeId,
    SpecPointer Location,
    DifferenceKind Kind,
    ValueSummary? Before,
    ValueSummary? After,
    ChangeImpact Impact,
    Confidence Confidence,
    string Rationale,
    string RecommendedAction,
    string? Policy = null);
