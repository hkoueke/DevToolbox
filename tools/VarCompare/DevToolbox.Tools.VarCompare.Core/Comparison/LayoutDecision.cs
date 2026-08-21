namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>Ce que le sélecteur de disposition a décidé, et pourquoi.</summary>
/// <param name="Layout">La disposition à afficher. Jamais <see cref="ComparisonLayout.Auto"/> : c'est résolu.</param>
/// <param name="WarnBeforeRendering">
/// S'il faut avertir le développeur avant que la disposition ne change sous ses yeux.
/// </param>
/// <param name="Reason">Une phrase expliquant le changement, montrée avec l'avertissement.</param>
public sealed record LayoutDecision(ComparisonLayout Layout, bool WarnBeforeRendering, string? Reason);
