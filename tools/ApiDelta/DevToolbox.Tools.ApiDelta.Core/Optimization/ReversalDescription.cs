namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Comment défaire une transformation.</summary>
/// <param name="Text">Ce qu'il faut faire pour revenir en arrière, dit à un lecteur.</param>
/// <param name="Reversible">Indique si le retour en arrière rend exactement l'état d'avant.</param>
/// <remarks>
/// Écrite au moment où la transformation est proposée, et non reconstituée après coup : c'est la
/// transformation qui sait ce qu'elle défait. Un manifeste qui dirait « réversible » sans savoir
/// comment ne serait qu'une assurance verbale.
/// </remarks>
public sealed record ReversalDescription(string Text, bool Reversible);
