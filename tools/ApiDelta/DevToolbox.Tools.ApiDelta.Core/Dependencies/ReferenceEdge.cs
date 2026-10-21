using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>Une référence, d'où elle part et vers quoi elle va.</summary>
/// <param name="From">Le nœud qui porte la référence.</param>
/// <param name="To">Ce qu'elle désigne.</param>
/// <param name="Resolution">Ce qu'il en est advenu.</param>
/// <param name="ResolvedLocation">Le nœud désigné, quand la référence se résout dans le document.</param>
public sealed record ReferenceEdge(
    SpecPointer From,
    ReferenceTarget To,
    ReferenceResolution Resolution,
    SpecPointer? ResolvedLocation = null);
