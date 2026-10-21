using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>
/// Rassemble des candidats : les doublons exacts par empreinte, les quasi-doublons par forme.
/// </summary>
/// <remarks>
/// Deux chemins, et la raison est arithmétique. Un doublon exact se trouve en une passe, parce que
/// l'empreinte est une clé. Un quasi-doublon, lui, demande une comparaison, et comparer tout avec tout
/// sur dix mille schémas fait cinquante millions de comparaisons d'arbres, qu'aucune optimisation ne
/// rattrape. On rassemble donc d'abord par une clé grossière, et on ne compare qu'à l'intérieur d'un
/// groupe — avec une borne, et l'aveu que la borne a été atteinte quand elle l'est.
/// </remarks>
public static class CandidateBuckets
{
    /// <summary>Regroupe les candidats canoniquement identiques.</summary>
    /// <param name="candidates">Les candidats.</param>
    /// <param name="fingerprinter">Donne l'empreinte.</param>
    /// <param name="profile">Le profil de comparaison.</param>
    /// <param name="version">La version déclarée par le document.</param>
    /// <returns>Les groupes de plus d'un membre, dans un ordre stable.</returns>
    public static IReadOnlyList<DuplicateGroup> Exact(
        IEnumerable<SpecNode> candidates,
        IFingerprinter fingerprinter,
        EquivalenceProfile profile,
        OpenApiVersion version)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(fingerprinter);

        return
        [
            .. candidates
                .GroupBy(candidate => fingerprinter.Fingerprint(candidate, profile, version), StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => new DuplicateGroup(group.Key, [.. group]))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Fingerprint, StringComparer.Ordinal),
        ];
    }

    /// <summary>Rassemble les candidats par forme, pour une comparaison deux à deux locale.</summary>
    /// <param name="candidates">Les candidats.</param>
    /// <param name="cap">La taille au-delà de laquelle un groupe n'est pas comparé.</param>
    /// <returns>Les groupes, et les formes laissées de côté.</returns>
    public static BucketedCandidates Near(IEnumerable<SpecNode> candidates, int cap)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap);

        List<IReadOnlyList<SpecNode>> buckets = [];
        List<ShapeKey> truncated = [];

        foreach (IGrouping<ShapeKey, SpecNode> bucket in candidates
            .GroupBy(ShapeKey.Of)
            .Where(bucket => bucket.Count() > 1)
            .OrderByDescending(bucket => bucket.Count())
            .ThenBy(bucket => bucket.Key.Properties, StringComparer.Ordinal))
        {
            if (bucket.Count() > cap)
            {
                truncated.Add(bucket.Key);
                continue;
            }

            buckets.Add([.. bucket]);
        }

        return new BucketedCandidates(buckets, truncated);
    }
}
