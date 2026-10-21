using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les paramètres écrits sur place et répétés à l'identique.
/// </summary>
/// <remarks>
/// C'est la répétition la plus courante dans une description qui a vécu, et la moins visible : chaque
/// opération a l'air raisonnable, et c'est l'ensemble qui pèse. Un groupe porte ses occurrences, leur
/// nombre et ce que l'extraction gagnerait, parce qu'une recommandation sans chiffre ne se tranche pas.
/// </remarks>
public sealed class RepeatedParameterRule : RedundancyRule
{
    /// <summary>Crée la règle.</summary>
    public RepeatedParameterRule()
        : base("AUD-100", "parameters", SpecObjectKind.Parameter)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IEnumerable<SpecNode> candidates = subject.Nodes(SpecObjectKind.Parameter)
            .Select(node => node.Node)
            .Where(node => !Nodes.IsUnderComponents(node.Location))
            .Where(node => !Nodes.IsReference(node));

        return RuleReport.Evaluated(
            Grouped(
                subject,
                candidates,
                "Le même paramètre est écrit",
                "L'extraire dans « components.parameters » et le référencer."));
    }

    /// <inheritdoc />
    protected override string Suggest(DuplicateGroup group) =>
        ComponentName.Suggest(
            Nodes.Text(group.Members[0], "name") ?? Nodes.Name(group.Members[0].Location),
            "Parametre");
}
