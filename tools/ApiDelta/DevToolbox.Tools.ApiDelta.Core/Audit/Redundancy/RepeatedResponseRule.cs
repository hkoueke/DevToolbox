using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Rapporte les réponses écrites sur place et répétées à l'identique.
/// </summary>
/// <remarks>
/// Les réponses d'erreur normalisées sont le cas type : la même réponse 400 recopiée dans chaque
/// opération, à la virgule près. Le constat nomme le code concerné, parce que « une réponse répétée
/// quatorze fois » n'aide personne à la retrouver.
/// </remarks>
public sealed class RepeatedResponseRule : RedundancyRule
{
    /// <summary>Crée la règle.</summary>
    public RepeatedResponseRule()
        : base("AUD-102", "responses", SpecObjectKind.Response)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IEnumerable<SpecNode> candidates = subject.Nodes(SpecObjectKind.Response)
            .Select(node => node.Node)
            .Where(node => !Nodes.IsUnderComponents(node.Location))
            .Where(node => !Nodes.IsReference(node));

        return RuleReport.Evaluated(
            Grouped(
                subject,
                candidates,
                "La même réponse est écrite",
                "L'extraire dans « components.responses » et la référencer."));
    }

    /// <inheritdoc />
    protected override string Suggest(DuplicateGroup group) =>
        ComponentName.Suggest("Reponse-" + Nodes.Name(group.Members[0].Location), "Reponse");
}
