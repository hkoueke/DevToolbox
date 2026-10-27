using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Ce qu'une règle de projection regarde.</summary>
/// <param name="Owner">Le nom de ce qui contient la construction, pour nommer un écart.</param>
/// <param name="Location">Où la construction se trouve.</param>
/// <param name="Type">Le type, si c'est un type qui est regardé.</param>
/// <param name="Particle">La particule, si c'en est une.</param>
/// <param name="Facet">La facette, si c'en est une.</param>
/// <param name="Attribute">L'attribut, si c'en est un.</param>
/// <param name="Binding">La liaison, si c'en est une.</param>
/// <param name="Operation">L'opération, si c'en est une.</param>
/// <param name="Version">La version qu'annonce la production, dont « nillable » dépend.</param>
/// <param name="KnownTypes">Les types nommés du contrat.</param>
/// <remarks>
/// Un porteur à membres facultatifs plutôt qu'une hiérarchie. Chaque règle de la table publiée regarde une
/// sorte de construction différente — une facette, une particule, un type, une liaison — et une règle par
/// ligne reste la propriété qui compte : une hiérarchie de sujets obligerait à répartir les lignes entre
/// plusieurs familles d'interfaces, et le lien avec la table publiée s'y perdrait.
/// </remarks>
public sealed record ProjectionSubject(
    string Owner,
    WsdlLocation Location,
    XsdType? Type = null,
    XsdParticle? Particle = null,
    XsdFacet? Facet = null,
    XsdAttributeUse? Attribute = null,
    WsdlBinding? Binding = null,
    WsdlOperation? Operation = null,
    Syntax.OpenApiVersion Version = Syntax.OpenApiVersion.V30,
    IReadOnlySet<string>? KnownTypes = null)
{
    /// <summary>
    /// Les types nommés du contrat, jamais nuls.
    /// </summary>
    /// <remarks>
    /// Une règle doit pouvoir distinguer un type du contrat — qui devient un renvoi — d'un type intégré
    /// de XSD — qui devient un « type » et un « format ». Sans cette table, elle ne pouvait faire ni
    /// l'un ni l'autre, et les propriétés sortaient sans type : un schéma dont les propriétés sont
    /// vides décrit un objet qui accepte n'importe quoi.
    /// </remarks>
    public IReadOnlySet<string> KnownTypes { get; init; } =
        KnownTypes ?? new HashSet<string>(StringComparer.Ordinal);
}
