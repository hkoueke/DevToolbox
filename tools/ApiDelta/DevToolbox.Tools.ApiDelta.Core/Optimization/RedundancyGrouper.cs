using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Rassemble en groupes chiffrés ce que les règles de répétition ont mesuré.
/// </summary>
/// <remarks>
/// Le chiffrage ne mesure pas lui-même, et c'est délibéré : seule la règle sait ce qu'elle a regardé. Un
/// en-tête répété dans quatorze opérations se mesure sur l'en-tête, non sur les opérations qui le
/// portent, et un chiffrage qui remesurerait d'après les emplacements du constat annoncerait quatorze
/// opérations entières comme économie possible.
/// <para>
/// Ne deviennent donc des groupes que les constats dont la règle a établi l'égalité <em>et</em> publié
/// ses deux mesures. Les autres restent des constats, où ils sont justes.
/// </para>
/// </remarks>
public static class RedundancyGrouper
{
    /// <summary>Ce que chaque règle de répétition rassemble.</summary>
    /// <remarks>
    /// La sorte sert à lire un rapport ; elle ne décide d'aucun remède. C'est pourquoi une règle absente
    /// de cette table n'est pas une règle sans groupe, mais une règle dont la sorte n'est pas nommée.
    /// </remarks>
    private static readonly Dictionary<string, CandidateKind> Kinds = new(StringComparer.Ordinal)
    {
        ["AUD-100"] = CandidateKind.Parameter,
        ["AUD-101"] = CandidateKind.Parameter,
        ["AUD-102"] = CandidateKind.Response,
        ["AUD-103"] = CandidateKind.RequestBody,
        ["AUD-104"] = CandidateKind.Schema,
        ["AUD-105"] = CandidateKind.Example,
        ["AUD-106"] = CandidateKind.Enumeration,

        // AUD-107 rapproche des composants de familles différentes : aucune sorte ne les couvre tous.
        ["AUD-107"] = CandidateKind.Other,
    };

    /// <summary>Chiffre les répétitions que l'examen a mesurées.</summary>
    /// <param name="audit">Ce que l'examen a constaté.</param>
    /// <returns>Les groupes, du plus rentable au moins rentable.</returns>
    public static IReadOnlyList<RedundancyGroup> Group(AuditResult audit)
    {
        ArgumentNullException.ThrowIfNull(audit);

        List<RedundancyGroup> groups = [];

        foreach (Finding finding in audit.Findings)
        {
            if (Of(finding) is { } group)
            {
                groups.Add(group);
            }
        }

        return
        [
            .. groups
                .OrderByDescending(group => group.Savings.AbsoluteSaving)
                .ThenBy(group => group.RuleId, StringComparer.Ordinal)
                .ThenBy(group => group.Location.Json, StringComparer.Ordinal),
        ];
    }

    /// <summary>Le groupe d'un constat, quand ce constat en est un.</summary>
    private static RedundancyGroup? Of(Finding finding)
    {
        if (finding.Evidence is not
            {
                Occurrences: > 1,
                Fingerprint: { } fingerprint,
                OccurrenceBytes: { } occurrenceBytes,
                ReferenceBytes: { } referenceBytes,
            } evidence)
        {
            return null;
        }

        IReadOnlyList<Occurrence> occurrences =
        [
            .. (finding.Locations.Count > 0 ? finding.Locations : [finding.Location])
                .Select(location => new Occurrence(location, occurrenceBytes)),
        ];

        return new RedundancyGroup(
            finding.RuleId,
            fingerprint,
            evidence.Profile ?? EquivalenceProfile.Strict.Name,
            Kinds.TryGetValue(finding.RuleId, out CandidateKind kind) ? kind : CandidateKind.Other,
            evidence.DifferingFields.Count == 0 ? DuplicateTier.Exact : DuplicateTier.Near,
            occurrences,
            SavingsEstimate.For(
                occurrenceBytes, occurrences.Count, referenceBytes, finding.Safety))
        {
            DifferingFields = evidence.DifferingFields,
            SuggestedComponentName = finding.SuggestedComponentName,
            StrictEquivalenceEstablished = finding.StrictEquivalenceEstablished,
        };
    }
}
