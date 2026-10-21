namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Une transformation que le plan proposait et que l'application n'a pas faite.</summary>
/// <param name="Transformation">Ce qui était proposé, dans son nouvel état.</param>
/// <param name="Reason">Pourquoi elle n'a pas eu lieu, dit à un lecteur.</param>
public sealed record DeclinedTransformation(Transformation Transformation, string Reason);
