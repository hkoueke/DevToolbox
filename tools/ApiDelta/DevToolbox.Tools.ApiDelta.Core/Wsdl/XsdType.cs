namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un type du jeu de schémas compilé, tel que la projection le lit.</summary>
/// <param name="Name">Son nom qualifié.</param>
/// <param name="Content">La façon dont il compose son contenu.</param>
/// <param name="BaseType">Le type dont il dérive, le cas échéant.</param>
/// <param name="Derivation">Comment il en dérive.</param>
/// <param name="Particles">Ses éléments, pour un contenu composé.</param>
/// <param name="Attributes">Ses attributs — aucun équivalent en OpenAPI, et la plus grosse source de trous.</param>
/// <param name="Facets">Ses facettes, pour un type simple restreint.</param>
/// <param name="Open">Il porte <c>xs:any</c> ou <c>xs:anyAttribute</c>.</param>
/// <param name="Abstract">Il est abstrait : ce sont ses membres concrets qui forment le contrat.</param>
/// <param name="SubstitutionMembers">Les membres de son groupe de substitution, s'il en est la tête.</param>
/// <param name="DependsOn">Les types qu'il atteint, cycles compris.</param>
/// <param name="Location">Où il est déclaré.</param>
/// <param name="UsesXsd11">
/// Indique que le sens de ce type dépend d'une construction de XSD 1.1.
/// </param>
/// <remarks>
/// Une forme de lecture, pas une forme d'écriture : elle porte ce dont la projection a besoin pour décider
/// ce qui se porte, ce qui ne se porte qu'approximativement et ce qui ne se porte pas. Rien ici ne sert à
/// réécrire un schéma, parce que rien n'écrit de WSDL.
/// </remarks>
public sealed record XsdType(
    string Name,
    XsdContentKind Content,
    string? BaseType,
    XsdDerivation Derivation,
    IReadOnlyList<XsdParticle> Particles,
    IReadOnlyList<XsdAttributeUse> Attributes,
    IReadOnlyList<XsdFacet> Facets,
    bool Open,
    bool Abstract,
    IReadOnlyList<string> SubstitutionMembers,
    IReadOnlyList<string> DependsOn,
    WsdlLocation Location,
    bool UsesXsd11 = false)
{
    /// <summary>Indique que ce type exige quelque chose qu'OpenAPI ne sait pas exprimer.</summary>
    /// <remarks>
    /// Calculé, et non renseigné : un type qui porterait des attributs tout en se déclarant entièrement
    /// portable serait un mensonge qu'aucun essai ne rattraperait.
    /// </remarks>
    public bool HasUncarryableParts =>
        Attributes.Count > 0
        || Content is XsdContentKind.Mixed
        || Abstract
        || SubstitutionMembers.Count > 0
        || Derivation is XsdDerivation.Restriction;
}
