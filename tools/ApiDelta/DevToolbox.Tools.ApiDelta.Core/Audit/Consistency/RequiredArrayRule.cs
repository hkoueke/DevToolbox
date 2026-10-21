using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les schémas qui se contredisent : une propriété exigée qui n'existe pas, une exigence
/// impossible, un tableau sans élément.
/// </summary>
/// <remarks>
/// Ce sont des erreurs, et non des négligences. Un nom dans <c>required</c> qui ne figure pas dans
/// <c>properties</c> est tenu pour une contrainte par les validateurs et ignoré par les engendreurs : la
/// description promet alors deux choses différentes selon l'outil qui la lit.
/// </remarks>
public sealed class RequiredArrayRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public RequiredArrayRule()
        : base("AUD-208", SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<AuditNode> schemas =
        [
            .. subject.Nodes(SpecObjectKind.Schema).Where(node => !Nodes.IsReference(node.Node)),
        ];

        return RuleReport.Evaluated(
        [
            .. Absent(schemas),
            .. Impossible(schemas),
            .. Itemless(schemas),
        ]);
    }

    /// <summary>Les noms exigés qui ne figurent pas parmi les propriétés.</summary>
    /// <remarks>
    /// Un schéma composé — <c>allOf</c>, <c>oneOf</c> — peut exiger une propriété déclarée ailleurs, et la
    /// règle ne le signale donc pas : elle ne regarde que les schémas qui déclarent leurs propriétés
    /// eux-mêmes.
    /// </remarks>
    private IEnumerable<Finding> Absent(IEnumerable<AuditNode> schemas) =>
        schemas
            .Where(schema => Nodes.Map(schema.Node, "properties") is not null)
            .Where(schema => !Composed(schema.Node))
            .SelectMany(schema => Nodes.Items(schema.Node, "required")
                .OfType<ScalarNode>()
                .Where(name => Nodes.Map(schema.Node, "properties")!.Find(name.Text) is null)
                .Select(name => Report(
                    $"La propriété « {name.Text} » est exigée mais n'est pas déclarée.",
                    name.Location,
                    "La déclarer, ou la retirer de « required ».")));

    /// <summary>Les propriétés à la fois exigées et déclarées nulles, ou exigées et en lecture seule.</summary>
    private IEnumerable<Finding> Impossible(IEnumerable<AuditNode> schemas) =>
        schemas
            .Where(schema => Nodes.Map(schema.Node, "properties") is not null)
            .SelectMany(schema => Nodes.Items(schema.Node, "required")
                .OfType<ScalarNode>()
                .Select(name => (name, Property: Nodes.Map(schema.Node, "properties")!.Find(name.Text)))
                .Where(pair => pair.Property is not null)
                .Where(pair => Nodes.Flag(pair.Property, "nullable") == true)
                .Select(pair => Report(
                    $"La propriété « {pair.name.Text} » est exigée et déclarée nulle à la fois.",
                    pair.Property!.Location,
                    "Choisir : exigée veut dire présente, nulle veut dire sans valeur ; les deux "
                        + "ensemble ne disent rien de vérifiable.")));

    /// <summary>Les tableaux dont le schéma des éléments manque.</summary>
    private IEnumerable<Finding> Itemless(IEnumerable<AuditNode> schemas) =>
        schemas
            .Where(schema => string.Equals(
                Nodes.Text(schema.Node, "type"), "array", StringComparison.Ordinal))
            .Where(schema => !Nodes.Has(schema.Node, "items")
                && !Nodes.Has(schema.Node, "prefixItems"))
            .Select(schema => Report(
                "Ce tableau ne dit pas ce qu'il contient.",
                schema.Location,
                "Déclarer « items » : un tableau sans élément décrit n'importe quoi."));

    private static bool Composed(SpecNode schema) =>
        Nodes.Has(schema, "allOf") || Nodes.Has(schema, "oneOf") || Nodes.Has(schema, "anyOf");
}
