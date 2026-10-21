using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Ce que les règles de répétition partagent : grouper par empreinte, et chiffrer ce que ça coûte.
/// </summary>
/// <remarks>
/// Un groupe est un seul constat portant toutes ses occurrences, et non une occurrence par constat. Un
/// en-tête identique écrit dans quatorze opérations est une décision à prendre, pas quatorze.
/// <para>
/// Le groupement se fait sous le profil le plus exigeant. Si la documentation était écartée, « identiques »
/// cesserait de vouloir dire « interchangeables », et extraire un composant commun jetterait la
/// description de l'une des occurrences sans le dire.
/// </para>
/// </remarks>
public abstract class RedundancyRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    /// <param name="ruleId">Son identifiant.</param>
    /// <param name="family">La famille de composants dans laquelle l'extraction rangerait le partage.</param>
    /// <param name="visits">Les sortes d'objets qu'elle regarde.</param>
    protected RedundancyRule(string ruleId, string family, params SpecObjectKind[] visits)
        : base(ruleId, visits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        Family = family;
    }

    /// <summary>La famille de composants visée par l'extraction.</summary>
    protected string Family { get; }

    /// <summary>Groupe des candidats identiques et compose un constat par groupe.</summary>
    /// <param name="subject">Le document.</param>
    /// <param name="candidates">Les candidats, déjà filtrés par la règle.</param>
    /// <param name="phrase">La phrase du constat, à laquelle le nombre d'occurrences est ajouté.</param>
    /// <param name="action">Ce qu'il est recommandé de faire.</param>
    /// <param name="safety">La sûreté, quand la règle doit la dégrader.</param>
    /// <param name="family">
    /// La famille visée, quand elle n'est pas celle de la règle : une même règle peut rassembler des
    /// exemples et des en-têtes, et la référence qui les remplacerait n'a pas la même longueur.
    /// </param>
    /// <returns>Un constat par groupe.</returns>
    protected IReadOnlyList<Finding> Grouped(
        AuditSubject subject,
        IEnumerable<SpecNode> candidates,
        string phrase,
        string action,
        SafetyClass? safety = null,
        string? family = null)
    {
        ArgumentNullException.ThrowIfNull(subject);

        int minimum = RuleSettingsResolver.MinimumOccurrences(subject.Ruleset.SettingFor(RuleId));

        return
        [
            .. CandidateBuckets
                .Exact(candidates, subject.Fingerprinter, EquivalenceProfile.Strict, subject.Version)
                .Where(group => group.Count >= minimum)
                .Select(group => Compose(group, phrase, action, safety, family ?? Family)),
        ];
    }

    /// <summary>Le nom de composant que la règle suggère pour un groupe.</summary>
    /// <param name="group">Le groupe.</param>
    /// <returns>Un nom assaini.</returns>
    protected virtual string Suggest(DuplicateGroup group) =>
        ComponentName.Suggest(Nodes.Name(group.Members[0].Location));

    private Finding Compose(
        DuplicateGroup group,
        string phrase,
        string action,
        SafetyClass? safety,
        string family)
    {
        string name = Suggest(group);
        int reference = Saving.ReferenceBytes(family, name);

        return Report(
            phrase + " " + group.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " fois à l'identique.",
            group.Members[0].Location,
            action,
            safety: safety) with
        {
            Locations = group.Locations,
            EstimatedBytesSaved = Saving.Bytes(group.OccurrenceBytes, group.Count, reference),
            SuggestedComponentName = name,

            // L'équivalence a été établie sous le profil exigeant : c'est ce qui autorise, et seulement
            // alors, un remède automatique.
            StrictEquivalenceEstablished = (safety ?? Catalogue.Safety) == SafetyClass.Safe,
            Evidence = new FindingEvidence(
                Occurrences: group.Count,
                Fingerprint: group.Fingerprint,
                Profile: EquivalenceProfile.Strict.Name,
                OccurrenceBytes: group.OccurrenceBytes,
                ReferenceBytes: reference),
        };
    }
}
