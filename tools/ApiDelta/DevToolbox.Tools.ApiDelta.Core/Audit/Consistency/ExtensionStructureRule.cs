using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les extensions de fournisseur dont la forme est connue et ne correspond pas.
/// </summary>
/// <remarks>
/// Connue, et seulement connue. Une extension dont la forme n'a pas été déclarée reste opaque : l'outil ne
/// dit rien de sa structure, ne la réécrit pas et ne la déplace pas. Supposer la forme d'une extension
/// inconnue serait la façon la plus directe de casser ce qu'une passerelle lit.
/// </remarks>
public sealed class ExtensionStructureRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public ExtensionStructureRule()
        : base("AUD-210", SpecObjectKind.Extension)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. subject.Nodes(SpecObjectKind.Extension)
                .Select(node => (node, Expected: Expected(subject, node)))
                .Where(pair => pair.Expected is not null)
                .SelectMany(pair => pair.Expected!
                    .Where(field => !Nodes.Has(pair.node.Node, field))
                    .Select(field => Report(
                        $"L'extension « {Nodes.Name(pair.node.Location)} » ne porte pas le champ "
                            + $"« {field} » que sa forme déclarée exige.",
                        pair.node.Location,
                        "Compléter l'extension, ou revoir la forme déclarée pour elle."))),
        ]);
    }

    /// <summary>Les champs qu'une extension doit porter, quand sa forme a été déclarée.</summary>
    private static IReadOnlyList<string>? Expected(AuditSubject subject, AuditNode node)
    {
        string name = Nodes.Name(node.Location);

        return node.Node is MappingNode
            && subject.Ruleset.KnownExtensionFields.TryGetValue(name, out IReadOnlyList<string>? fields)
                ? fields
                : null;
    }
}
