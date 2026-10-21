using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les énumérations identiques, sans jamais proposer de les fusionner.
/// </summary>
/// <remarks>
/// Deux énumérations aux mêmes valeurs ne sont pas forcément la même énumération : un statut de dossier et
/// un statut de document peuvent se croiser aujourd'hui et diverger demain. Les réunir fait une
/// dépendance entre deux notions qui n'en avaient pas, et c'est pourquoi le remède passe toujours en
/// relecture.
/// </remarks>
public sealed class DuplicateEnumRule : RedundancyRule
{
    /// <summary>Crée la règle.</summary>
    public DuplicateEnumRule()
        : base("AUD-106", "schemas", SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IEnumerable<SpecNode> enumerations = subject.Nodes(SpecObjectKind.Schema)
            .Select(node => Nodes.Sequence(node.Node, "enum"))
            .OfType<SpecNode>();

        return RuleReport.Evaluated(
            Grouped(
                subject,
                enumerations,
                "Le même jeu de valeurs énumérées est écrit",
                "Vérifier si les deux notions sont la même ; ne les réunir que si elles le sont.",
                SafetyClass.ReviewRequired));
    }

    /// <inheritdoc />
    protected override string Suggest(DuplicateGroup group) =>
        ComponentName.Suggest(
            string.Join('-', group.Members[0].Location.Segments.TakeLast(2)), "Enumeration");
}
