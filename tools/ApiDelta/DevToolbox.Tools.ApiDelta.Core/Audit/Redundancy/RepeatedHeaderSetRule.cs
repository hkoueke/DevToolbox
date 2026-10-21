using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les groupes d'en-têtes que plusieurs opérations portent à l'identique.
/// </summary>
/// <remarks>
/// Un groupe, et non un en-tête : c'est le cas où la bonne réponse n'est pas d'extraire chaque en-tête
/// séparément mais de les remonter au niveau du chemin, ou de les déclarer une fois ensemble. Un seul
/// en-tête répété relève de la règle qui précède, et ne compte pas ici.
/// <para>
/// La sûreté n'est acquise que si la remontée garde le même sens : un en-tête déclaré au niveau du chemin
/// vaut pour toutes ses opérations, y compris celles qui ne le portaient pas. La règle ne peut l'établir
/// que lorsque toutes les opérations du chemin portent le même groupe, et dégrade sinon.
/// </para>
/// </remarks>
public sealed class RepeatedHeaderSetRule : RedundancyRule
{
    private const int MinimumHeaders = 2;

    /// <summary>Crée la règle.</summary>
    public RepeatedHeaderSetRule()
        : base("AUD-101", "parameters", SpecObjectKind.Operation)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        int minimum = RuleSettingsResolver.MinimumOccurrences(subject.Ruleset.SettingFor(RuleId));

        return RuleReport.Evaluated(
        [
            .. subject.Nodes(SpecObjectKind.Operation)
                .Select(operation => (operation, Headers: Headers(operation.Node)))
                .Where(pair => pair.Headers.Count >= MinimumHeaders)
                .Select(pair => (pair.operation, pair.Headers, Key: Key(subject, pair.Headers)))
                .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                .Where(group => group.Count() >= minimum)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Compose(group.Key, [.. group])),
        ]);
    }

    /// <summary>Les en-têtes écrits sur place par une opération.</summary>
    private static IReadOnlyList<SpecNode> Headers(SpecNode operation) =>
    [
        .. Nodes.Items(operation, "parameters")
            .Where(parameter => !Nodes.IsReference(parameter))
            .Where(parameter => string.Equals(
                Nodes.Text(parameter, "in"), "header", StringComparison.Ordinal)),
    ];

    /// <summary>
    /// L'identité d'un groupe d'en-têtes : les empreintes de ses membres, triées et jointes.
    /// </summary>
    /// <remarks>
    /// Triées, parce que l'ordre d'écriture des paramètres ne dit rien : deux opérations qui déclarent les
    /// mêmes en-têtes dans un ordre différent déclarent la même chose.
    /// </remarks>
    private static string Key(AuditSubject subject, IReadOnlyList<SpecNode> headers) =>
        string.Join(
            '|',
            headers
                .Select(header => subject.Fingerprinter.Fingerprint(
                    header, EquivalenceProfile.Strict, subject.Version))
                .OrderBy(fingerprint => fingerprint, StringComparer.Ordinal));

    private Finding Compose(
        string key,
        IReadOnlyList<(AuditNode Operation, IReadOnlyList<SpecNode> Headers, string Key)> group)
    {
        IReadOnlyList<SpecPointer> locations = [.. group.Select(member => member.Operation.Location)];
        IReadOnlyList<SpecNode> headers = group[0].Headers;

        long occurrence = SizeMeasurement.Total(headers);
        string name = ComponentName.Suggest(
            string.Join('-', headers.Select(header => Nodes.Text(header, "name") ?? "en-tete")),
            "EnTetes");

        // Une référence par en-tête du groupe : les remonter au niveau du chemin en laisse autant.
        int reference = Saving.ReferenceBytes(Family, name) * headers.Count;

        return Report(
            $"Les mêmes {headers.Count} en-têtes sont écrits à l'identique dans {group.Count} opérations.",
            locations[0],
            "Les déclarer une fois : au niveau du chemin lorsque toutes ses opérations les portent, "
                + "sinon dans « components.parameters ».",

            // Remonter au niveau du chemin ajouterait ces en-têtes aux opérations qui ne les portent pas :
            // l'équivalence de portée reste à vérifier, et c'est au relecteur de le faire.
            safety: SafetyClass.ReviewRequired) with
        {
            Locations = locations,
            EstimatedBytesSaved = Saving.Bytes(occurrence, group.Count, reference),
            SuggestedComponentName = name,
            Evidence = new FindingEvidence(
                Occurrences: group.Count,
                Fingerprint: key,
                Profile: EquivalenceProfile.Strict.Name,
                OccurrenceBytes: occurrence,
                ReferenceBytes: reference),
        };
    }
}
