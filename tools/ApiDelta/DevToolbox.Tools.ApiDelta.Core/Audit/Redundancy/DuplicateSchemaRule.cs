using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les schémas en double, les exacts et les presque, dans deux groupes séparés.
/// </summary>
/// <remarks>
/// Séparés, parce que les deux ne se traitent pas de la même façon. Un double exact est un fait ; un
/// quasi-double est une ressemblance, qui peut être une négligence comme une distinction voulue. Les
/// mélanger obligerait le lecteur à vérifier chaque ligne pour savoir laquelle des deux il a devant lui.
/// <para>
/// Un double sous deux noms différents n'est jamais sûr : les engendreurs de clients tirent les noms de
/// types des noms de schémas, si bien que fusionner deux noms renomme un type chez l'appelant.
/// </para>
/// </remarks>
public sealed class DuplicateSchemaRule : RedundancyRule
{
    private const int MaxDifferingFields = 2;

    /// <summary>Crée la règle.</summary>
    public DuplicateSchemaRule()
        : base("AUD-104", "schemas", SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<SpecNode> named =
        [
            .. subject.Nodes(SpecObjectKind.Schema)
                .Select(node => node.Node)
                .Where(node => Nodes.IsComponent(node.Location, "schemas"))
                .Where(node => !Nodes.IsReference(node)),
        ];

        IReadOnlyList<Finding> exact = Exact(subject, named);
        BucketedCandidates buckets = CandidateBuckets.Near(named, subject.Ruleset.MaxNearDuplicateBucket);
        IReadOnlyList<Finding> near = Near(subject, buckets);

        IReadOnlyList<Finding> findings = [.. exact, .. near];

        return buckets.IsComplete
            ? RuleReport.Evaluated(findings)
            : RuleReport.Truncated(
                findings,
                $"{buckets.Truncated.Count} groupe(s) de schémas dépassaient la borne de comparaison et "
                    + "n'ont pas été examinés pour les quasi-doublons.");
    }

    /// <summary>Les doubles exacts.</summary>
    private IReadOnlyList<Finding> Exact(AuditSubject subject, IReadOnlyList<SpecNode> named) =>
    [
        .. CandidateBuckets
            .Exact(named, subject.Fingerprinter, EquivalenceProfile.Strict, subject.Version)
            .Select(group => Identical(group)),
    ];

    private Finding Identical(DuplicateGroup group)
    {
        IReadOnlyList<string> names = [.. group.Members.Select(member => Nodes.Name(member.Location))];
        bool sameName = names.Distinct(StringComparer.Ordinal).Count() == 1;
        int reference = Saving.ReferenceBytes(Family, names[0]);

        return Report(
            $"Les schémas {string.Join(", ", names.Select(name => "« " + name + " »"))} "
                + "sont identiques au mot près.",
            group.Members[0].Location,
            sameName
                ? "N'en garder qu'un."
                : "N'en garder qu'un, après avoir vérifié l'effet du renommage sur les clients engendrés.",
            safety: sameName ? SafetyClass.Safe : SafetyClass.ReviewRequired) with
        {
            Locations = group.Locations,
            EstimatedBytesSaved = Saving.Bytes(group.OccurrenceBytes, group.Count, reference),
            SuggestedComponentName = ComponentName.Suggest(names[0], "Schema"),
            StrictEquivalenceEstablished = sameName,
            Evidence = new FindingEvidence(
                Occurrences: group.Count,
                Fingerprint: group.Fingerprint,
                Profile: EquivalenceProfile.Strict.Name,
                OccurrenceBytes: group.OccurrenceBytes,
                ReferenceBytes: reference),
        };
    }

    /// <summary>Les quasi-doublons, comparés deux à deux à l'intérieur d'un groupe de même forme.</summary>
    private List<Finding> Near(AuditSubject subject, BucketedCandidates buckets)
    {
        List<Finding> findings = [];

        foreach (IReadOnlyList<SpecNode> bucket in buckets.Buckets)
        {
            for (int first = 0; first < bucket.Count; first++)
            {
                for (int second = first + 1; second < bucket.Count; second++)
                {
                    if (Similar(subject, bucket[first], bucket[second]) is { } finding)
                    {
                        findings.Add(finding);
                    }
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Indique si deux schémas se ressemblent assez pour que la ressemblance veuille dire quelque chose.
    /// </summary>
    /// <remarks>
    /// Deux objets qui ne diffèrent que par <c>properties</c> et <c>required</c> peuvent n'avoir
    /// strictement rien en commun : c'est le cas de deux schémas quelconques du même document. La
    /// ressemblance ne compte donc que si les deux déclarent les <em>mêmes</em> propriétés, et que la
    /// différence est à l'intérieur de l'une d'elles.
    /// </remarks>
    private static bool Comparable(SpecNode left, SpecNode right, IReadOnlyList<string> differing)
    {
        if (!differing.Contains("properties", StringComparer.Ordinal))
        {
            return true;
        }

        return Names(left).SequenceEqual(Names(right), StringComparer.Ordinal);
    }

    /// <summary>Les noms de propriétés d'un schéma, triés.</summary>
    private static IEnumerable<string> Names(SpecNode schema) =>
        (Nodes.Map(schema, "properties")?.Entries ?? [])
            .Select(entry => entry.Key.Text)
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>Le constat d'une ressemblance, quand deux schémas ne diffèrent que par peu de champs.</summary>
    private Finding? Similar(AuditSubject subject, SpecNode left, SpecNode right)
    {
        string ours = subject.Fingerprinter.Fingerprint(
            left, EquivalenceProfile.Strict, subject.Version);
        string theirs = subject.Fingerprinter.Fingerprint(
            right, EquivalenceProfile.Strict, subject.Version);

        if (string.Equals(ours, theirs, StringComparison.Ordinal))
        {
            // Déjà rapporté comme double exact.
            return null;
        }

        IReadOnlyList<string> differing = FieldDifference.Between(
            left, right, subject.Fingerprinter, subject.Version);

        if (differing.Count == 0 || differing.Count > MaxDifferingFields || !Comparable(left, right, differing))
        {
            return null;
        }

        return Report(
            $"Les schémas « {Nodes.Name(left.Location)} » et « {Nodes.Name(right.Location)} » "
                + "ne diffèrent que par " + string.Join(" et ", differing) + ".",
            left.Location,
            "Vérifier si la différence est voulue ; si elle ne l'est pas, n'en garder qu'un.",
            FindingSeverity.Information,
            SafetyClass.Unsafe) with
        {
            Locations = [left.Location, right.Location],

            // Mesuré, mais pas recommandé : le chiffrage dira ce que la fusion vaudrait, et la sûreté
            // dira qu'elle n'est pas établie. Taire la mesure obligerait à la refaire à la main pour
            // décider si la ressemblance vaut qu'on s'y arrête.
            Evidence = new FindingEvidence(
                Occurrences: 2,
                Fingerprint: ours,
                DifferingFields: differing,
                Profile: EquivalenceProfile.Strict.Name,
                Reasoning: "Rapprochement fondé sur une forme commune, non sur une équivalence établie.",
                OccurrenceBytes: SizeMeasurement.Largest([left, right]),
                ReferenceBytes: Saving.ReferenceBytes(
                    Family, Nodes.Name(left.Location))),
        };
    }
}
