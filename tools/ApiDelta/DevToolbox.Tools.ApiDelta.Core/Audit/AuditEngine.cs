using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>
/// Fait tourner les règles d'audit en une seule traversée de l'arbre.
/// </summary>
/// <remarks>
/// Une passe, et des règles réveillées par la sorte d'objet qu'elles regardent. Quarante-sept règles qui
/// parcourraient chacune un arbre de quinze mégaoctets feraient d'un travail de cinq secondes un travail
/// d'une minute et demie, pour le même résultat.
/// <para>
/// Le moteur rend un sort pour chaque règle enregistrée, y compris celles qui n'ont rien trouvé et celles
/// qui n'ont pas pu tourner. C'est la discipline de varcompare — indéterminé n'est pas absent — portée
/// dans un second outil : un audit sans constat n'est une bonne nouvelle que si toutes les règles ont
/// tourné.
/// </para>
/// </remarks>
public sealed class AuditEngine : IAuditEngine
{
    private readonly IReadOnlyList<IAuditRule> _rules;
    private readonly IMetamodel _metamodel;
    private readonly IFingerprinter _fingerprinter;

    /// <summary>Crée le moteur.</summary>
    /// <param name="rules">Les règles enregistrées.</param>
    /// <param name="metamodel">Dit ce qu'est un emplacement.</param>
    /// <param name="fingerprinter">Donne l'empreinte d'un sous-arbre.</param>
    public AuditEngine(
        IEnumerable<IAuditRule> rules,
        IMetamodel metamodel,
        IFingerprinter fingerprinter)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(metamodel);
        ArgumentNullException.ThrowIfNull(fingerprinter);

        // Triées par identifiant : l'ordre d'enregistrement ne doit pas pouvoir changer un rapport.
        _rules = [.. rules.OrderBy(rule => rule.RuleId, StringComparer.Ordinal)];
        _metamodel = metamodel;
        _fingerprinter = fingerprinter;
    }

    /// <inheritdoc />
    public AuditResult Run(AuditRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AuditSubject subject = new(
            request.Document,
            request.Ruleset,
            Walk(request.Document, cancellationToken),
            _fingerprinter,
            request.References,
            request.StructuralVerdicts);

        List<Finding> findings = [];
        List<RuleOutcome> outcomes = [];

        foreach (IAuditRule rule in _rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (RuleOutcome outcome, IReadOnlyList<Finding> produced) = Apply(rule, subject);

            outcomes.Add(outcome);
            findings.AddRange(produced);
        }

        return new AuditResult(Order(findings), outcomes);
    }

    /// <summary>Fait tourner une règle, et traduit ce qu'elle rend en un sort et des constats.</summary>
    private static (RuleOutcome Outcome, IReadOnlyList<Finding> Findings) Apply(
        IAuditRule rule,
        AuditSubject subject)
    {
        RuleSetting setting = subject.Ruleset.SettingFor(rule.RuleId);

        if (!setting.Enabled)
        {
            return (new RuleOutcome(rule.RuleId, RuleState.Disabled, 0), []);
        }

        RuleReport report = Evaluate(rule, subject);

        IReadOnlyList<Finding> adjusted = RuleSettingsResolver.Resolve(report.Findings, setting);

        if (adjusted.Count > subject.Ruleset.MaxFindingsPerRule)
        {
            int kept = subject.Ruleset.MaxFindingsPerRule;

            return (
                new RuleOutcome(
                    rule.RuleId,
                    RuleState.Truncated,
                    kept,
                    $"La règle a trouvé {adjusted.Count} constats et n'en rend que {kept}."),
                [.. adjusted.Take(kept)]);
        }

        return (
            new RuleOutcome(rule.RuleId, report.State, adjusted.Count, report.Reason),
            adjusted);
    }

    /// <summary>
    /// Fait tourner une règle, et tient pour non évaluée celle qui échoue.
    /// </summary>
    /// <remarks>
    /// Une règle qui lève n'emporte pas l'audit avec elle : les quarante-six autres ont des choses à dire.
    /// Mais elle ne compte pas non plus pour propre, et son sort le dit.
    /// </remarks>
    private static RuleReport Evaluate(IAuditRule rule, AuditSubject subject)
    {
        if (!rule.Visits.Any(kind => subject.Nodes(kind).Count > 0))
        {
            return RuleReport.Clean;
        }

        try
        {
            return rule.Evaluate(subject);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or ArgumentException or FormatException or KeyNotFoundException or OverflowException)
        {
            return RuleReport.NotEvaluated(
                "La règle n'a pas pu s'exécuter sur cette description : " + exception.Message);
        }
    }

    /// <summary>Range les constats dans un ordre que deux exécutions partagent.</summary>
    private static IReadOnlyList<Finding> Order(IEnumerable<Finding> findings) =>
    [
        .. findings
            .OrderBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Location.Json, StringComparer.Ordinal)
            .ThenBy(finding => finding.Message, StringComparer.Ordinal),
    ];

    /// <summary>Parcourt l'arbre une fois, et range ses nœuds par sorte d'objet.</summary>
    private Dictionary<SpecObjectKind, IReadOnlyList<AuditNode>> Walk(
        SpecDocument document,
        CancellationToken cancellationToken)
    {
        Dictionary<SpecObjectKind, List<AuditNode>> collected = [];
        Stack<SpecNode> pending = new();

        pending.Push(document.Root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SpecNode node = pending.Pop();
            MetamodelEntry entry = _metamodel.Resolve(node.Location, document.DeclaredVersion);
            SpecObjectKind kind = OwnKind(entry);

            if (!collected.TryGetValue(kind, out List<AuditNode>? bucket))
            {
                bucket = [];
                collected[kind] = bucket;
            }

            bucket.Add(new AuditNode(node, kind, entry));

            Descend(node, pending);
        }

        return collected.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<AuditNode>)Ordered(pair.Value));
    }

    /// <summary>
    /// Empile les enfants d'un nœud à l'envers, pour que la pile les rende dans l'ordre d'écriture.
    /// </summary>
    private static void Descend(SpecNode node, Stack<SpecNode> pending)
    {
        switch (node)
        {
            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries.Reverse())
                {
                    pending.Push(entry.Value);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items.Reverse())
                {
                    pending.Push(item);
                }

                break;
        }
    }

    /// <summary>Range les nœuds d'une sorte par emplacement, pour que l'ordre ne dépende pas de la pile.</summary>
    private static List<AuditNode> Ordered(List<AuditNode> nodes)
    {
        nodes.Sort((left, right) => string.CompareOrdinal(
            left.Location.Json, right.Location.Json));

        return nodes;
    }

    /// <summary>
    /// La sorte du nœud lui-même, par opposition à celle de ce qu'il contient.
    /// </summary>
    /// <remarks>
    /// La table décrit un champ par ce qu'il contient : <c>responses</c> annonce une réponse. Le nœud
    /// porté par ce champ est donc le conteneur, non le membre, et les confondre rendrait les deux sous la
    /// même sorte — une règle qui regarde les réponses verrait l'association et chacune de ses réponses.
    /// </remarks>
    private static SpecObjectKind OwnKind(MetamodelEntry entry) =>
        entry.Collection == CollectionKind.None
            ? entry.ChildKind
            : Container(entry.ChildKind);

    private static SpecObjectKind Container(SpecObjectKind member) => member switch
    {
        SpecObjectKind.PathItem => SpecObjectKind.Paths,
        SpecObjectKind.Response => SpecObjectKind.Responses,
        _ => SpecObjectKind.Unknown,
    };
}
