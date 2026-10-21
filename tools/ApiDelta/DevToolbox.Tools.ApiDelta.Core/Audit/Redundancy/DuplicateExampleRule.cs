using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les exemples et les en-têtes de réponse écrits sur place et répétés à l'identique.
/// </summary>
/// <remarks>
/// La moins urgente des répétitions, et c'est pourquoi elle n'est qu'une information : un exemple recopié
/// n'engage rien. Elle vaut tout de même d'être dite, parce qu'un exemple recopié se met tôt ou tard à
/// diverger de ses copies, et qu'un lecteur ne saura pas laquelle croire.
/// </remarks>
public sealed class DuplicateExampleRule : RedundancyRule
{
    /// <summary>Crée la règle.</summary>
    public DuplicateExampleRule()
        : base("AUD-105", "examples", SpecObjectKind.Example, SpecObjectKind.Header)
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
                Inline(subject, SpecObjectKind.Example),
                "Le même exemple est écrit",
                "L'extraire dans « components.examples » et le référencer."),
            .. Grouped(
                subject,
                Inline(subject, SpecObjectKind.Header),
                "Le même en-tête de réponse est écrit",
                "L'extraire dans « components.headers » et le référencer.",
                family: "headers"),
        ]);
    }

    /// <inheritdoc />
    protected override string Suggest(DuplicateGroup group) =>
        ComponentName.Suggest(Nodes.Name(group.Members[0].Location), "Exemple");

    private static IEnumerable<SpecNode> Inline(AuditSubject subject, SpecObjectKind kind) =>
        subject.Nodes(kind)
            .Select(node => node.Node)
            .Where(node => !Nodes.IsUnderComponents(node.Location))
            .Where(node => !Nodes.IsReference(node));
}
