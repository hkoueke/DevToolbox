namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Une entrée d'association : sa clé telle qu'écrite, et sa valeur.</summary>
/// <param name="Key">La clé, dont on conserve le style d'écriture et la position.</param>
/// <param name="Value">La valeur associée.</param>
public sealed record MappingEntry(ScalarNode Key, SpecNode Value);
