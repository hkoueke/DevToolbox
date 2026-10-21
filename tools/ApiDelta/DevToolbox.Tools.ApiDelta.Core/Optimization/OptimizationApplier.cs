using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Applique à une copie ce qu'un plan a proposé, et seulement ce que l'outil peut prouver ou ce qu'on lui
/// a nommément permis.
/// </summary>
/// <remarks>
/// Trois refus, et aucun n'est négociable. Une transformation que l'outil ne sait pas prouver sûre n'est
/// appliquée que si son identifiant a été explicitement autorisé. Une transformation seulement signalée
/// ne l'est jamais, quelle que soit l'autorisation. Et un objet portant une extension sous un préfixe
/// protégé n'est pas touché : l'outil ne sait pas ce qu'une passerelle lit, et déplacer cette donnée
/// serait affirmer une équivalence qu'il ne peut pas établir.
/// <para>
/// Rien n'est appliqué sur le document d'origine. L'arbre édité est construit à côté, sérialisé, puis
/// relu et comparé — et c'est seulement si cette comparaison aboutit que des octets existent à écrire.
/// </para>
/// <para>
/// Une transformation qu'une règle refuse n'emporte pas les autres. Elle est rendue écartée, avec sa
/// raison, et l'arbre reste celui d'avant elle : un plan de quatre propositions dont une échoue produit
/// une copie portant les trois autres, et le dit.
/// </para>
/// </remarks>
public sealed class OptimizationApplier : IOptimizationApplier
{
    private readonly Dictionary<string, IOptimizationRule> _rules;
    private readonly ISpecEmitter _emitter;
    private readonly IContractEquivalenceVerifier _verifier;

    /// <summary>Crée l'applicateur.</summary>
    /// <param name="rules">Les règles d'optimisation enregistrées.</param>
    /// <param name="emitter">Sérialise l'arbre édité.</param>
    /// <param name="verifier">Établit que la sortie dit la même chose.</param>
    public OptimizationApplier(
        IEnumerable<IOptimizationRule> rules,
        ISpecEmitter emitter,
        IContractEquivalenceVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentNullException.ThrowIfNull(verifier);

        _rules = rules.ToDictionary(rule => rule.RuleId, StringComparer.Ordinal);
        _emitter = emitter;
        _verifier = verifier;
    }

    /// <inheritdoc />
    public OptimizationOutcome Apply(
        OptimizationPlan plan,
        SpecDocument production,
        RunSelection selection,
        Ruleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(ruleset);

        SpecNode edited = production.Root;
        List<Transformation> applied = [];
        List<DeclinedTransformation> declined = [];
        List<SpecPointer> touched = [];

        foreach (Transformation transformation in Ordered(plan))
        {
            if (Refused(transformation, selection, ruleset) is { } reason)
            {
                declined.Add(Decline(transformation, TransformationState.Rejected, reason));
                continue;
            }

            if (Overlaps(touched, transformation))
            {
                declined.Add(Decline(
                    transformation,
                    TransformationState.Rejected,
                    "Une transformation déjà appliquée a traité ces emplacements : les deux remèdes "
                    + "visent la même répétition, et ils ne se cumulent pas."));

                continue;
            }

            if (!Guarded(edited, transformation, ruleset, _rules[transformation.RuleId].Removes))
            {
                declined.Add(Decline(
                    transformation,
                    TransformationState.Blocked,
                    "Cet objet porte une donnée de fournisseur que l'outil ne sait pas évaluer : "
                    + "il ne la déplace pas et ne la jette pas."));

                continue;
            }

            Result<SpecNode> result = _rules[transformation.RuleId].Apply(edited, transformation);

            if (result.IsFailure)
            {
                declined.Add(Decline(
                    transformation, TransformationState.Blocked, result.Failure!.Message));

                continue;
            }

            edited = result.Value;
            touched.AddRange(transformation.Origins);
            applied.Add(transformation with { State = TransformationState.Applied });
        }

        return Finish(edited, production, ruleset, applied, declined);
    }

    /// <summary>Sérialise, contrôle la survie des extensions, puis vérifie l'équivalence.</summary>
    /// <remarks>
    /// La survie des extensions est contrôlée sur l'arbre édité, et l'équivalence sur les octets. Les
    /// deux ensemble couvrent OPT-010 : une extension perdue par une édition est vue ici, et une
    /// extension altérée par l'écriture est vue là — le profil du contrat compte les extensions, puisque
    /// l'outil ne sait pas ce qu'elles valent et ne peut donc pas les tenir pour négligeables.
    /// </remarks>
    private OptimizationOutcome Finish(
        SpecNode edited,
        SpecDocument production,
        Ruleset ruleset,
        List<Transformation> applied,
        List<DeclinedTransformation> declined)
    {
        if (applied.Count == 0)
        {
            return new OptimizationOutcome(
                ReadOnlyMemory<byte>.Empty, [], declined, VerificationResult.NotRun);
        }

        IReadOnlyList<string> lost = ExtensionProtection.Survived(production.Root, edited);

        if (lost.Count > 0)
        {
            // Les appliquées restent appliquées : elles l'ont été sur l'arbre, et c'est ce qui permet à
            // l'étape d'écriture de distinguer « rien n'était applicable » de « ce qui a été appliqué ne
            // peut pas être publié ». Ce qui ne sortira pas, ce sont les octets.
            return new OptimizationOutcome(
                ReadOnlyMemory<byte>.Empty,
                applied,
                declined,
                new VerificationResult(
                    false,
                    false,
                    false,
                    false,
                    [.. lost.Select(name => "extension perdue : " + name)]));
        }

        ReadOnlyMemory<byte> serialized = _emitter.Emit(edited, ruleset.Formatting);
        VerificationResult verification = _verifier.Verify(serialized, production, ruleset);

        return new OptimizationOutcome(
            verification.Established ? serialized : ReadOnlyMemory<byte>.Empty,
            applied,
            declined,
            verification);
    }

    /// <summary>
    /// Pourquoi une transformation n'a pas le droit d'être appliquée, s'il y a une raison.
    /// </summary>
    private string? Refused(
        Transformation transformation,
        RunSelection selection,
        Ruleset ruleset)
    {
        if (!_rules.TryGetValue(transformation.RuleId, out IOptimizationRule? rule))
        {
            return "Aucune règle enregistrée ne sait appliquer " + transformation.RuleId + ".";
        }

        if (!OptimizationDefaults.Enabled(rule.RuleId, ruleset))
        {
            return "La règle " + transformation.RuleId + " n'est pas active.";
        }

        return transformation.Safety switch
        {
            SafetyClass.Safe => null,

            SafetyClass.ReviewRequired when selection.AllowedReviewRequiredRules.Contains(
                transformation.RuleId, StringComparer.Ordinal) => null,

            SafetyClass.ReviewRequired =>
                "La règle " + transformation.RuleId + " demande une relecture et n'a pas été autorisée "
                + "nommément.",

            _ => "Cette transformation est seulement signalée : elle ne s'applique pas.",
        };
    }

    /// <summary>Indique qu'aucune origine ne porte d'extension protégée.</summary>
    private static bool Guarded(
        SpecNode root,
        Transformation transformation,
        Ruleset ruleset,
        bool removing) =>
        transformation.Origins.All(origin =>
            PointerNavigator.Find(root, origin.Json) is not { } node
            || ExtensionProtection.Guards(node, ruleset, removing));

    /// <summary>
    /// Indique si une transformation touche des emplacements qu'une autre a déjà traités.
    /// </summary>
    /// <remarks>
    /// Deux remèdes peuvent viser la même répétition sans se cumuler : extraire chaque paramètre répété
    /// et remonter le jeu d'en-têtes au niveau du chemin sont des alternatives, et la seconde appliquée
    /// après la première ne trouverait plus que des références. L'un des deux doit donc céder, et le
    /// cédant est écarté en le disant — non silencieusement raté.
    /// <para>
    /// La comparaison porte sur la contenance et non sur l'égalité : les origines d'une remontée sont des
    /// opérations, celles d'une extraction sont les paramètres qui vivent dedans.
    /// </para>
    /// </remarks>
    private static bool Overlaps(IReadOnlyList<SpecPointer> touched, Transformation transformation) =>
        transformation.Origins.Any(origin =>
            touched.Any(already => already.Contains(origin) || origin.Contains(already)));

    /// <summary>
    /// L'ordre d'application : le prouvé avant le relu, le plus profitable avant le reste.
    /// </summary>
    /// <remarks>
    /// La sûreté d'abord, et c'est la décision qui compte ici : quand deux remèdes visent la même
    /// répétition, celui que l'outil sait prouver l'emporte sur celui qui demande une relecture. Ordonner
    /// par identifiant de règle aurait donné le même résultat sur le corpus actuel, mais par accident de
    /// numérotation.
    /// </remarks>
    private static IEnumerable<Transformation> Ordered(OptimizationPlan plan) =>
        plan.Transformations
            .OrderBy(transformation => transformation.Safety == SafetyClass.Safe ? 0 : 1)
            .ThenByDescending(transformation => transformation.EstimatedBytesSaved)
            .ThenBy(transformation => transformation.RuleId, StringComparer.Ordinal)
            .ThenBy(
                transformation => transformation.Origins.Count > 0
                    ? transformation.Origins[0].Json
                    : string.Empty,
                StringComparer.Ordinal);

    private static DeclinedTransformation Decline(
        Transformation transformation,
        TransformationState state,
        string reason) =>
        new(transformation with { State = state }, reason);
}
