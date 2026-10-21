using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Un nœud, avec ce que le métamodèle en sait.</summary>
/// <param name="Node">Le nœud.</param>
/// <param name="Kind">La sorte d'objet OpenAPI qu'il désigne.</param>
/// <param name="Entry">Ce que la table en sait.</param>
/// <remarks>
/// La passe unique résout le métamodèle une fois par nœud et le transmet. Quarante-sept règles qui le
/// résoudraient chacune pour elle-même feraient quarante-sept traversées d'un arbre de quinze mégaoctets.
/// </remarks>
public sealed record AuditNode(SpecNode Node, SpecObjectKind Kind, MetamodelEntry Entry)
{
    /// <summary>Où le nœud se trouve.</summary>
    public SpecPointer Location => Node.Location;

    /// <summary>Le nœud, vu comme une association, quand il en est une.</summary>
    public MappingNode? Mapping => Node as MappingNode;
}
