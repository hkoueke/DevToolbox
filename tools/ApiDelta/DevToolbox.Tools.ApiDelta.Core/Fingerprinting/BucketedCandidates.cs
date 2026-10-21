using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>Des candidats rassemblés par forme, et ce que le rassemblement a dû laisser de côté.</summary>
/// <param name="Buckets">Les groupes comparables entre eux, du plus grand au plus petit.</param>
/// <param name="Truncated">Les formes dont le groupe dépassait la borne, et n'a donc pas été comparé.</param>
/// <remarks>
/// La troncature est portée par le résultat, non consignée dans un journal. Un groupe que l'outil n'a pas
/// comparé ne doit pas se présenter comme un groupe sans quasi-doublon : annoncer propre ce qu'on n'a pas
/// regardé est le seul défaut qu'un audit ne peut pas avoir.
/// </remarks>
public sealed record BucketedCandidates(
    IReadOnlyList<IReadOnlyList<SpecNode>> Buckets,
    IReadOnlyList<ShapeKey> Truncated)
{
    /// <summary>Indique si la comparaison a été complète.</summary>
    public bool IsComplete => Truncated.Count == 0;
}
