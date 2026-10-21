using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les corps de requête et les types de média écrits sur place et répétés à l'identique.
/// </summary>
/// <remarks>
/// Les deux sont comptés séparément, parce que les remèdes diffèrent : un corps entier s'extrait dans
/// <c>components.requestBodies</c>, tandis qu'un type de média répété signale le plus souvent un schéma à
/// partager.
/// </remarks>
public sealed class RepeatedRequestBodyRule : RedundancyRule
{
    /// <summary>Crée la règle.</summary>
    public RepeatedRequestBodyRule()
        : base("AUD-103", "requestBodies", SpecObjectKind.RequestBody, SpecObjectKind.MediaType)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. Grouped(
                subject,
                Inline(subject, SpecObjectKind.RequestBody),
                "Le même corps de requête est écrit",
                "L'extraire dans « components.requestBodies » et le référencer."),
            .. Grouped(
                subject,
                Inline(subject, SpecObjectKind.MediaType),
                "Le même type de média est écrit",
                "Partager le schéma qu'il décrit, plutôt que de le réécrire."),
        ]);
    }

    /// <inheritdoc />
    protected override string Suggest(DuplicateGroup group) =>
        ComponentName.Suggest(
            string.Join('-', group.Members[0].Location.Segments.TakeLast(2)), "Corps");

    private static IEnumerable<SpecNode> Inline(AuditSubject subject, SpecObjectKind kind) =>
        subject.Nodes(kind)
            .Select(node => node.Node)
            .Where(node => !Nodes.IsUnderComponents(node.Location))
            .Where(node => !Nodes.IsReference(node));
}
