using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Versioning;

/// <summary>Un changement qui a porté la recommandation à son niveau.</summary>
/// <param name="Location">Où il se trouve.</param>
/// <param name="ChangeId">De quelle sorte il est.</param>
/// <remarks>
/// Nommer les contributeurs est ce qui rend la recommandation discutable : « majeure » n'apprend rien,
/// « majeure parce que ce paramètre est devenu obligatoire » s'examine et, le cas échéant, se conteste.
/// </remarks>
public sealed record VersionContributor(SpecPointer Location, string ChangeId);
