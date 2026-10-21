using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Une écriture d'une chose répétée, et ce qu'elle occupe.</summary>
/// <param name="Location">Où elle se trouve.</param>
/// <param name="Bytes">Ce qu'elle occupe, mesuré sur son étendue dans le fichier.</param>
public sealed record Occurrence(SpecPointer Location, long Bytes);
