using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les composants qui ne diffèrent que par leur documentation.
/// </summary>
/// <remarks>
/// Le cas le plus franc du double déguisé : deux composants que le contrat ne distingue pas, et que seules
/// une description ou un exemple séparent. Le constat nomme les champs qui diffèrent, parce que c'est par
/// là que le lecteur décidera s'il s'agit d'un oubli ou d'une distinction voulue.
/// <para>
/// Le remède reste en relecture. Réunir les deux fait disparaître l'une des deux descriptions, et ce n'est
/// pas à un outil d'en choisir la survivante.
/// </para>
/// </remarks>
public sealed class EquivalentAliasRule : RedundancyRule
{
    /// <summary>Crée la règle.</summary>
    public EquivalentAliasRule()
        : base(
            "AUD-107",
            "schemas",
            SpecObjectKind.Schema,
            SpecObjectKind.Parameter,
            SpecObjectKind.Response,
            SpecObjectKind.RequestBody,
            SpecObjectKind.Header)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        List<Finding> findings = [];

        foreach (IGrouping<string, SpecNode> family in Components(subject)
            .GroupBy(node => node.Location.Segments[1], StringComparer.Ordinal)
            .OrderBy(family => family.Key, StringComparer.Ordinal))
        {
            findings.AddRange(Aliases(subject, family.Key, [.. family]));
        }

        return RuleReport.Evaluated(findings);
    }

    /// <summary>Les composants nommés, toutes familles confondues.</summary>
    private IEnumerable<SpecNode> Components(AuditSubject subject) =>
        Visits
            .Distinct()
            .SelectMany(subject.Nodes)
            .Select(node => node.Node)
            .Where(node => node.Location.Segments.Count == 3)
            .Where(node => Nodes.IsUnderComponents(node.Location))
            .Where(node => !Nodes.IsReference(node));

    /// <summary>
    /// Les composants d'une même famille que le profil du contrat ne distingue pas, mais que le profil
    /// exigeant distingue : la différence est donc ailleurs que dans le contrat.
    /// </summary>
    private IEnumerable<Finding> Aliases(
        AuditSubject subject,
        string family,
        IReadOnlyList<SpecNode> members) =>
        members
            .GroupBy(member => subject.Fingerprinter.Fingerprint(
                member, EquivalenceProfile.Contract, subject.Version), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Where(group => group
                .Select(member => subject.Fingerprinter.Fingerprint(
                    member, EquivalenceProfile.Strict, subject.Version))
                .Distinct(StringComparer.Ordinal)
                .Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Alias(subject, family, [.. group]));

    private Finding Alias(AuditSubject subject, string family, IReadOnlyList<SpecNode> members)
    {
        IReadOnlyList<string> names = [.. members.Select(member => Nodes.Name(member.Location))];
        IReadOnlyList<string> differing = FieldDifference.Between(
            members[0], members[1], subject.Fingerprinter, subject.Version);

        return Report(
            $"Dans « {family} », {string.Join(", ", names.Select(name => "« " + name + " »"))} "
                + "ne diffèrent que par " + Named(differing) + ".",
            members[0].Location,
            "Décider s'il s'agit d'un double : si oui, n'en garder qu'un, en conservant la description "
                + "qui vaut la peine d'être lue.") with
        {
            Locations = [.. members.Select(member => member.Location)],

            // Mesuré comme les autres répétitions : le chiffrage doit pouvoir dire ce que ce double
            // déguisé coûte, même si le remède reste une décision humaine.
            Evidence = new FindingEvidence(
                Occurrences: members.Count,
                Fingerprint: subject.Fingerprinter.Fingerprint(
                    members[0], EquivalenceProfile.Contract, subject.Version),
                DifferingFields: differing,
                Profile: EquivalenceProfile.Contract.Name,
                OccurrenceBytes: SizeMeasurement.Largest(members),
                ReferenceBytes: Saving.ReferenceBytes(family, names[0])),
        };
    }

    private static string Named(IReadOnlyList<string> differing) =>
        differing.Count == 0 ? "des champs hors contrat" : string.Join(" et ", differing);
}
