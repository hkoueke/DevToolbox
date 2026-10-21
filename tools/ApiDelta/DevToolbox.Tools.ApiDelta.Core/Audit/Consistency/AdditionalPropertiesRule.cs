using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les objets dont l'ouverture n'est pas décidée de la même façon dans tout le document.
/// </summary>
/// <remarks>
/// <c>additionalProperties</c> absent veut dire « ouvert », et beaucoup d'auteurs croient avoir dit
/// « fermé ». Quand une partie du document le déclare et l'autre non, les clients engendrés diffèrent d'un
/// schéma à l'autre sans que rien ne l'explique.
/// <para>
/// La règle ne dit rien d'un document qui ne le déclare jamais : c'est un choix, et il est cohérent. Elle
/// ne parle que du mélange, et elle nomme la minorité.
/// </para>
/// </remarks>
public sealed class AdditionalPropertiesRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public AdditionalPropertiesRule()
        : base("AUD-209", SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<AuditNode> objects =
        [
            .. subject.Nodes(SpecObjectKind.Schema)
                .Where(schema => !Nodes.IsReference(schema.Node))
                .Where(schema => string.Equals(
                    Nodes.Text(schema.Node, "type"), "object", StringComparison.Ordinal)),
        ];

        IReadOnlyList<AuditNode> stated =
        [
            .. objects.Where(schema => Nodes.Has(schema.Node, "additionalProperties")),
        ];

        if (stated.Count == 0 || stated.Count == objects.Count)
        {
            return RuleReport.Clean;
        }

        IReadOnlyList<AuditNode> silent =
        [
            .. objects.Where(schema => !Nodes.Has(schema.Node, "additionalProperties")),
        ];

        return RuleReport.Evaluated(
        [
            .. silent.Select(schema => Report(
                "Cet objet ne dit pas s'il accepte d'autres propriétés, alors que "
                    + stated.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " autres le disent.",
                schema.Location,
                "Déclarer « additionalProperties » comme les autres, ou le retirer partout.") with
            {
                Evidence = new FindingEvidence(
                    Occurrences: objects.Count,
                    Reasoning: "Déduit du mélange des deux écritures dans le même document."),
            }),
        ]);
    }
}
