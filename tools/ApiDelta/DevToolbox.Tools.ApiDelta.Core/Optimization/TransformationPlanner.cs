using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Transforme en propositions chiffrées les répétitions que l'examen a établies.
/// </summary>
/// <remarks>
/// La famille de composants où irait l'extraction est lue sur l'emplacement de la chose répétée, et non
/// sur la règle qui l'a trouvée. La différence n'est pas cosmétique : une même règle rassemble les corps
/// de requête et les types de média, si bien qu'y attacher une famille ferait un jour proposer de ranger
/// un type de média de réponse dans <c>components.requestBodies</c>, et de produire une description qui
/// ne se relit pas.
/// <para>
/// Un emplacement d'où aucune famille ne se lit ne donne donc pas de proposition. C'est ce qui garde
/// silencieux ce que l'outil ne sait pas faire : partager le schéma d'un type de média répété est un bon
/// conseil, mais ce n'est pas une transformation qu'il sait écrire, et le constat le dit déjà.
/// </para>
/// </remarks>
public static class TransformationPlanner
{
    /// <summary>La règle qui rapporte un groupe d'en-têtes porté par plusieurs opérations.</summary>
    private const string HeaderSetRule = "AUD-101";

    /// <summary>
    /// La règle d'optimisation qui traite chaque famille de composants.
    /// </summary>
    /// <remarks>
    /// <c>schemas</c> n'y figure pas : extraire un schéma écrit sur place est OPT-006, désactivée par
    /// défaut parce qu'elle renomme des types chez les appelants. L'inscrire ici avec un réglage qui vaut
    /// « activée » par défaut la rendrait active sans que personne ne l'ait demandé.
    /// </remarks>
    private static readonly Dictionary<string, string> Rules = new(StringComparer.Ordinal)
    {
        ["parameters"] = "OPT-002",
        ["responses"] = "OPT-003",
        ["requestBodies"] = "OPT-004",
        ["headers"] = "OPT-005",
        ["examples"] = "OPT-005",
    };

    /// <summary>Ce qu'une extraction fait, et comment on la défait, par famille.</summary>
    private static readonly Dictionary<string, (string Summary, string Reversal)> Wording =
        new(StringComparer.Ordinal)
        {
            ["parameters"] = (
                "Extraire le paramètre répété dans « components.parameters » et le référencer.",
                "réinsérer le paramètre à chaque origine, puis supprimer le composant"),

            ["responses"] = (
                "Extraire la réponse répétée dans « components.responses » et la référencer.",
                "réinsérer la réponse à chaque origine, puis supprimer le composant"),

            ["requestBodies"] = (
                "Extraire le corps de requête répété dans « components.requestBodies » et le référencer.",
                "réinsérer le corps à chaque origine, puis supprimer le composant"),

            ["headers"] = (
                "Extraire l'en-tête de réponse répété dans « components.headers » et le référencer.",
                "réinsérer l'en-tête à chaque origine, puis supprimer le composant"),

            ["examples"] = (
                "Extraire l'exemple répété dans « components.examples » et le référencer.",
                "réinsérer l'exemple à chaque origine, puis supprimer le composant"),
        };

    /// <summary>Chiffre le nettoyage, sans rien nettoyer.</summary>
    /// <param name="production">La description examinée.</param>
    /// <param name="audit">Ce que l'examen a constaté.</param>
    /// <param name="ruleset">Ce que l'équipe a décidé.</param>
    /// <returns>Le plan.</returns>
    public static OptimizationPlan Plan(
        SpecDocument production,
        AuditResult audit,
        Ruleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(ruleset);

        IReadOnlyList<RedundancyGroup> groups = RedundancyGrouper.Group(audit);

        List<Transformation> proposals = [];

        foreach (RedundancyGroup group in groups)
        {
            if (Propose(group, ruleset) is { } proposal)
            {
                proposals.Add(proposal);
            }
        }

        proposals.AddRange(FromFindings(audit, ruleset));

        return new OptimizationPlan(
            groups,
            proposals,
            GrowthCheck.For(
                production.ByteLength,
                proposals.Sum(proposal => proposal.EstimatedBytesSaved),
                ruleset.MaxGrowthPercent));
    }

    /// <summary>
    /// Les propositions qui ne viennent pas d'une répétition mais d'un constat isolé.
    /// </summary>
    /// <remarks>
    /// Deux règles ne se chiffrent pas en groupes : retirer un composant inaccessible et poser un drapeau
    /// de dépréciation. Elles procèdent d'un constat chacune, et leur sûreté vient de ce constat — AUD-003
    /// sait si l'analyse des références était complète, et c'est cela qui décide si la suppression est
    /// sûre ou demande une relecture. Le plan ne la redécide pas.
    /// </remarks>
    private static IEnumerable<Transformation> FromFindings(AuditResult audit, Ruleset ruleset)
    {
        foreach (Finding finding in audit.Findings)
        {
            string? ruleId = finding.RuleId switch
            {
                "AUD-003" => "OPT-007",
                "AUD-205" => "OPT-009",
                _ => null,
            };

            if (ruleId is null || !OptimizationDefaults.Enabled(ruleId, ruleset))
            {
                continue;
            }

            yield return Isolated(finding, ruleId);
        }
    }

    /// <summary>La transformation d'un constat isolé.</summary>
    private static Transformation Isolated(Finding finding, string ruleId) =>
        new(
            ruleId,
            finding.Safety,
            [finding.Location],
            Destination: null,
            Evidence: null,
            new ReversalDescription(
                string.Equals(ruleId, "OPT-007", StringComparison.Ordinal)
                    ? "réinsérer le composant tel qu'il était"
                    : "retirer le drapeau de dépréciation posé",
                Reversible: true),
            TransformationState.Proposed)
        {
            Summary = string.Equals(ruleId, "OPT-007", StringComparison.Ordinal)
                ? "Retirer le composant qu'aucun chemin n'atteint."
                : "Poser « deprecated: true » là où la description annonce déjà la dépréciation.",
            FromRuleId = finding.RuleId,
            EstimatedBytesSaved = finding.EstimatedBytesSaved ?? 0,
            ReviewReason = finding.Safety == SafetyClass.Safe ? null : finding.RecommendedAction,
        };

    /// <summary>La proposition d'un groupe, quand il y en a une.</summary>
    private static Transformation? Propose(RedundancyGroup group, Ruleset ruleset)
    {
        if (group.Tier != DuplicateTier.Exact || !group.Savings.Recommended)
        {
            return null;
        }

        return string.Equals(group.RuleId, HeaderSetRule, StringComparison.Ordinal)
            ? Hoist(group, ruleset)
            : Extract(group, ruleset);
    }

    /// <summary>
    /// L'extraction d'une chose répétée dans la famille de composants que son emplacement désigne.
    /// </summary>
    private static Transformation? Extract(RedundancyGroup group, Ruleset ruleset)
    {
        if (Family(group.Location) is not { } family
            || !Rules.TryGetValue(family, out string? ruleId)
            || !OptimizationDefaults.Enabled(ruleId, ruleset)
            || group.SuggestedComponentName is not { } name)
        {
            return null;
        }

        // Un composant ne s'extrait pas dans les composants, et des occurrences de familles différentes
        // ne se rangent pas au même endroit. Les deux viseraient une destination qui n'a pas de sens.
        if (group.Occurrences.Any(occurrence =>
                IsComponent(occurrence.Location)
                || !string.Equals(Family(occurrence.Location), family, StringComparison.Ordinal)))
        {
            return null;
        }

        (string summary, string reversal) = Wording[family];

        return Build(group, ruleId, summary, reversal)
            with
        {
            Destination = SpecPointer.Root.Child("components").Child(family).Child(name),
        };
    }

    /// <summary>La remontée d'en-têtes communs au niveau du chemin.</summary>
    /// <remarks>
    /// Aucune destination : une remontée déplace, elle ne crée pas de composant. Lui en inventer une
    /// laisserait croire qu'un composant apparaîtra là où rien n'apparaîtra.
    /// </remarks>
    private static Transformation? Hoist(RedundancyGroup group, Ruleset ruleset) =>
        OptimizationDefaults.Enabled("OPT-008", ruleset)
            ? Build(
                group,
                "OPT-008",
                "Remonter les en-têtes communs au niveau du chemin, là où la portée ne change pas.",
                "redescendre les en-têtes dans chaque opération qui les portait")
            : null;

    private static Transformation Build(
        RedundancyGroup group,
        string ruleId,
        string summary,
        string reversal) =>
        new(
            ruleId,
            group.Safety,
            group.Locations,
            Destination: null,
            group.StrictEquivalenceEstablished ? group.Fingerprint : null,
            new ReversalDescription(reversal, Reversible: true),
            TransformationState.Proposed)
        {
            Summary = summary,
            FromRuleId = group.RuleId,
            EstimatedBytesSaved = group.Savings.AbsoluteSaving,
            BreakEvenOccurrences = group.Savings.BreakEvenOccurrences,
            ReviewReason = group.Safety == SafetyClass.Safe
                ? null
                : "L'outil n'a pas pu établir que ce remède laisse les appelants inchangés.",
        };

    /// <summary>
    /// La famille de composants que désigne un emplacement, ou rien quand il n'en désigne aucune.
    /// </summary>
    private static string? Family(SpecPointer location)
    {
        IReadOnlyList<string> segments = location.Segments;

        if (segments.Count == 0)
        {
            return null;
        }

        return segments[^1] switch
        {
            "requestBody" => "requestBodies",
            "example" => "examples",
            _ => segments.Count >= 2 ? Parent(segments[^2]) : null,
        };
    }

    /// <summary>La famille qu'annonce la clé sous laquelle une chose est écrite.</summary>
    private static string? Parent(string key) => key switch
    {
        "parameters" => "parameters",
        "responses" => "responses",
        "headers" => "headers",
        "examples" => "examples",
        "requestBodies" => "requestBodies",
        "schemas" => "schemas",
        _ => null,
    };

    /// <summary>Indique si un emplacement est celui d'un composant nommé.</summary>
    private static bool IsComponent(SpecPointer location) =>
        location.Segments.Count > 0
        && string.Equals(location.Segments[0], "components", StringComparison.Ordinal);
}
