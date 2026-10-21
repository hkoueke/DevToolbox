using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Versioning;

/// <summary>
/// Recommande la version à publier, et nomme ce qui l'a commandée.
/// </summary>
/// <remarks>
/// Le point de départ est la version de la production, jamais celle du candidat : c'est ce qui est publié
/// qui fixe le point de départ, et un candidat engendré porte souvent un numéro qui ne veut rien dire.
/// </remarks>
public sealed class VersionRecommender : IVersionPolicy
{
    private const string VersionField = "version";

    private const string InfoField = "info";

    /// <inheritdoc />
    public VersionRecommendation Recommend(
        IReadOnlyList<Difference> differences,
        SpecDocument production,
        VersioningPolicy policy,
        SemanticVersion? requested = null,
        bool forced = false)
    {
        ArgumentNullException.ThrowIfNull(differences);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(policy);

        IReadOnlyDictionary<string, VersionImpact> resolved = IncrementPolicy.Resolved(policy);

        (SemanticVersion? baseline, string? problem) = ReadBaseline(production, policy);

        VersionImpact increment = VersionImpact.None;
        List<VersionContributor> contributors = [];
        List<VersionContributor> unreviewed = [];

        foreach (Difference difference in differences)
        {
            VersionImpact candidate = IncrementPolicy.Of(difference, policy);

            if (difference.Impact == ChangeImpact.Unknown)
            {
                unreviewed.Add(Describe(difference));
            }

            if (candidate > increment)
            {
                increment = candidate;
                contributors.Clear();
            }

            if (candidate == increment && candidate != VersionImpact.None)
            {
                contributors.Add(Describe(difference));
            }
        }

        SemanticVersion? recommended = baseline?.Increment(increment);

        return new VersionRecommendation(
            baseline,
            problem,
            increment,
            recommended,
            contributors,
            unreviewed.Count > 0,
            unreviewed,
            resolved,
            Override(requested, recommended, forced));
    }

    /// <summary>
    /// Lit la version de la production, ou dit pourquoi elle n'a pas pu l'être.
    /// </summary>
    /// <remarks>
    /// Un repli n'est employé que si l'équipe en a explicitement désigné un. Sans cela, l'outil préfère
    /// annoncer qu'il ne sait pas d'où partir : recommander « 1.0.1 » à partir d'une version illisible
    /// serait une réponse que personne ne peut vérifier.
    /// </remarks>
    private static (SemanticVersion? Baseline, string? Problem) ReadBaseline(
        SpecDocument production,
        VersioningPolicy policy)
    {
        string? declared = (production.Root as MappingNode)
            ?.Find(InfoField) is MappingNode info
                ? (info.Find(VersionField) as ScalarNode)?.Text
                : null;

        if (SemanticVersion.TryParse(declared, out SemanticVersion? parsed))
        {
            return (parsed, null);
        }

        if (SemanticVersion.TryParse(policy.FallbackBaseline, out SemanticVersion? fallback))
        {
            return (
                fallback,
                declared is null
                    ? "La production ne déclare pas de version ; le repli configuré est employé."
                    : $"La version « {declared} » n'est pas une version sémantique ; le repli configuré "
                        + "est employé.");
        }

        return (
            null,
            declared is null
                ? "La production ne déclare pas de version, et aucun repli n'est configuré : il n'y a pas "
                    + "de point de départ à incrémenter."
                : $"La version « {declared} » n'est pas une version sémantique, et aucun repli n'est "
                    + "configuré : il n'y a pas de point de départ à incrémenter.");
    }

    private static VersionOverride? Override(
        SemanticVersion? requested,
        SemanticVersion? recommended,
        bool forced)
    {
        if (requested is null)
        {
            return null;
        }

        bool below = recommended is not null && requested.CompareRanks(recommended) < 0;

        return new VersionOverride(requested, below, forced);
    }

    private static VersionContributor Describe(Difference difference) =>
        new(difference.Location, difference.ChangeId);
}
