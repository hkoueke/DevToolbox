using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>
/// Met en sourdine ce qu'une équipe a décidé de taire, et dit ce qu'elle a oublié de relire.
/// </summary>
/// <remarks>
/// Trois comportements, et chacun existe contre une façon précise de se tromper.
/// <para>
/// Un constat mis en sourdine est <em>écarté du seuil d'échec, non du rapport</em>. Le faire disparaître
/// rendrait le fichier de sourdines invérifiable : personne ne pourrait dire, en lisant un rapport, ce
/// que l'équipe a choisi de ne pas voir.
/// </para>
/// <para>
/// Une sourdine échue n'est pas honorée, et son échéance est dite. C'est tout l'intérêt d'avoir une
/// échéance : une sourdine qui continuerait de valoir après sa date serait une sourdine sans date.
/// </para>
/// <para>
/// Une sourdine qui n'apparie rien est signalée. C'est d'ordinaire un emplacement corrigé ou renommé, et
/// la laisser en place sans rien dire est la façon dont un fichier de sourdines devient de
/// l'archéologie.
/// </para>
/// </remarks>
public static class SuppressionApplier
{
    /// <summary>Applique les sourdines aux constats d'un examen.</summary>
    /// <param name="findings">Les constats, tels que les règles les ont rendus.</param>
    /// <param name="suppressions">Ce que l'équipe a décidé de taire.</param>
    /// <param name="today">Le jour de l'exécution, pour les échéances.</param>
    /// <returns>Ce qui reste, ce qui est tu, et ce qu'il faut relire.</returns>
    public static SuppressionOutcome Apply(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<Suppression> suppressions,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(suppressions);

        if (suppressions.Count == 0)
        {
            return new SuppressionOutcome(findings, [], [], []);
        }

        List<Finding> remaining = [];
        List<SuppressedFinding> silenced = [];
        HashSet<Suppression> matched = [];
        HashSet<Suppression> expired = [];

        foreach (Finding finding in findings)
        {
            Suppression? applicable = null;

            foreach (Suppression suppression in suppressions.Where(
                suppression => Covers(suppression, finding)))
            {
                matched.Add(suppression);

                if (suppression.HasExpired(today))
                {
                    expired.Add(suppression);
                    continue;
                }

                applicable ??= suppression;
            }

            if (applicable is null)
            {
                remaining.Add(finding);
                continue;
            }

            silenced.Add(new SuppressedFinding(finding, applicable));
        }

        return new SuppressionOutcome(
            remaining,
            silenced,
            Ordered(expired),
            Ordered(suppressions.Where(suppression => !matched.Contains(suppression))));
    }

    /// <summary>Indique si une sourdine couvre un constat.</summary>
    private static bool Covers(Suppression suppression, Finding finding) =>
        string.Equals(suppression.RuleId, finding.RuleId, StringComparison.Ordinal)
        && PointerGlob.Matches(suppression.Location, finding.Location.Json);

    private static IReadOnlyList<Suppression> Ordered(IEnumerable<Suppression> suppressions) =>
    [
        .. suppressions
            .OrderBy(suppression => suppression.RuleId, StringComparer.Ordinal)
            .ThenBy(suppression => suppression.Location, StringComparer.Ordinal),
    ];
}
