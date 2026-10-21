using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les tournures de nullabilité qui n'appartiennent pas à la version déclarée.
/// </summary>
/// <remarks>
/// Les deux versions expriment la même idée autrement, et la tournure de l'autre version n'est pas une
/// erreur de syntaxe : elle est simplement ignorée. Un champ annoncé nullable qui ne l'est pas pour les
/// outils est pire qu'un champ non annoncé, parce que l'auteur croit l'avoir dit.
/// </remarks>
public sealed class NullabilityVersionRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public NullabilityVersionRule()
        : base("AUD-008", SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<AuditNode> schemas = subject.Nodes(SpecObjectKind.Schema);

        return RuleReport.Evaluated(
        [
            .. subject.Version == OpenApiVersion.V31
                ? schemas.Where(schema => Nodes.Has(schema.Node, "nullable")).Select(Legacy)
                : schemas.Where(schema => UnionWithNull(schema.Node)).Select(Union),
        ]);
    }

    private Finding Legacy(AuditNode schema) =>
        Report(
            "Le schéma emploie « nullable », qui n'existe plus en 3.1 et y est ignoré.",
            schema.Location,
            "Écrire « type: [<type>, 'null'] » à la place.");

    private Finding Union(AuditNode schema) =>
        Report(
            "Le schéma emploie un type en union avec « null », qui n'existe pas en 3.0 et y est ignoré.",
            schema.Location,
            "Écrire « nullable: true » à la place, ou déclarer la description en 3.1.");

    /// <summary>Indique si le schéma déclare son type comme une union contenant <c>null</c>.</summary>
    private static bool UnionWithNull(SpecNode schema) =>
        Nodes.Sequence(schema, "type") is { } types
        && types.Items.OfType<ScalarNode>().Any(item =>
            string.Equals(item.Text, "null", StringComparison.Ordinal));
}
