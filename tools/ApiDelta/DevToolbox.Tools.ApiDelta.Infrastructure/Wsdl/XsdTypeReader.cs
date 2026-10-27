using System.Xml;
using System.Xml.Schema;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Relève un jeu de schémas compilé sous la forme que la projection sait lire.
/// </summary>
/// <remarks>
/// Après compilation, et c'est tout l'intérêt : le modèle post-compilation de <see cref="XmlSchemaSet"/>
/// a déjà résolu les dérivations, aplati les groupes et attaché les facettes héritées. Le lire avant
/// compilation obligerait à refaire ce travail, et à le refaire moins bien.
/// <para>
/// Une forme de lecture seulement. Rien ici ne sert à réécrire un schéma, parce que rien n'écrit de WSDL.
/// </para>
/// </remarks>
internal static class XsdTypeReader
{
    /// <summary>Relève les éléments globaux et le type de chacun.</summary>
    /// <param name="set">Le jeu compilé.</param>
    /// <returns>Les éléments, par nom qualifié.</returns>
    internal static IReadOnlyDictionary<string, string> Elements(XmlSchemaSet set)
    {
        Dictionary<string, string> elements = new(StringComparer.Ordinal);

        foreach (XmlSchemaElement element in set.GlobalElements.Values.OfType<XmlSchemaElement>())
        {
            if (element.QualifiedName.IsEmpty)
            {
                continue;
            }

            XmlQualifiedName? named = element.ElementSchemaType?.QualifiedName;

            if (named is null || named.IsEmpty)
            {
                continue;
            }

            elements[element.QualifiedName.ToString()] = named.ToString();
        }

        return elements;
    }

    /// <summary>Relève les types globaux du jeu.</summary>
    /// <param name="set">Le jeu compilé.</param>
    /// <param name="document">Le document d'entrée, pour les emplacements.</param>
    /// <returns>Les types, par nom qualifié.</returns>
    internal static IReadOnlyDictionary<string, XsdType> Read(XmlSchemaSet set, string document)
    {
        Dictionary<string, XsdType> types = new(StringComparer.Ordinal);

        foreach (XmlSchemaType type in set.GlobalTypes.Values.OfType<XmlSchemaType>())
        {
            // Les types intégrés de XSD ne sont pas le contrat : « anyType » y figurait, et la projection
            // l'aurait parcouru comme un type du service.
            if (type.QualifiedName.IsEmpty
                || string.Equals(
                    type.QualifiedName.Namespace, WsdlNamespaces.Schema, StringComparison.Ordinal))
            {
                continue;
            }

            types[type.QualifiedName.ToString()] = Describe(type, document, set);
        }

        return types;
    }

    private static XsdType Describe(XmlSchemaType type, string document, XmlSchemaSet set) =>
        type switch
        {
            XmlSchemaComplexType complex => Complex(complex, document, set),
            XmlSchemaSimpleType simple => Simple(simple, document),
            _ => Empty(type, document),
        };

    private static XsdType Complex(XmlSchemaComplexType type, string document, XmlSchemaSet set)
    {
        List<XsdParticle> particles = [];

        Collect(type.ContentTypeParticle, document, particles);

        return new XsdType(
            type.QualifiedName.ToString(),
            Kind(type),
            type.BaseXmlSchemaType?.QualifiedName.IsEmpty == false
                ? type.BaseXmlSchemaType.QualifiedName.ToString()
                : null,
            Derivation(type),
            particles,
            [.. Attributes(type, document)],
            [],
            IsOpen(type),
            type.IsAbstract,
            [.. Substitutes(type, set)],
            [.. particles.Select(particle => particle.TypeName).Distinct(StringComparer.Ordinal)],
            At(type, document));
    }

    private static XsdType Simple(XmlSchemaSimpleType type, string document) =>
        new(
            type.QualifiedName.ToString(),
            XsdContentKind.Simple,
            type.BaseXmlSchemaType?.QualifiedName.IsEmpty == false
                ? type.BaseXmlSchemaType.QualifiedName.ToString()
                : null,
            Derivation(type),
            [],
            [],
            [.. Facets(type)],
            false,
            false,
            [],
            [],
            At(type, document));

    private static XsdType Empty(XmlSchemaType type, string document) =>
        new(
            type.QualifiedName.ToString(),
            XsdContentKind.Simple,
            null,
            XsdDerivation.None,
            [],
            [],
            [],
            false,
            false,
            [],
            [],
            At(type, document));

    /// <summary>La façon dont un type complexe compose son contenu.</summary>
    private static XsdContentKind Kind(XmlSchemaComplexType type)
    {
        if (type.IsMixed)
        {
            return XsdContentKind.Mixed;
        }

        return type.ContentTypeParticle switch
        {
            XmlSchemaAll => XsdContentKind.All,
            XmlSchemaChoice => XsdContentKind.Choice,
            XmlSchemaSequence => XsdContentKind.Sequence,
            _ => XsdContentKind.Simple,
        };
    }

    /// <summary>
    /// Comment un type dérive d'un autre — et « de rien » quand il ne dérive de rien.
    /// </summary>
    /// <remarks>
    /// Le piège que la première version de ce lecteur avait manqué : <c>DerivedBy</c> rend
    /// <c>Restriction</c> pour un type complexe ordinaire, parce que tout type dérive implicitement
    /// d'<c>anyType</c> par restriction. Lu tel quel, cela faisait rendre
    /// <see cref="XsdType.HasUncarryableParts"/> vrai pour <em>chaque</em> type du contrat, et la
    /// projection aurait déclaré non portable tout ce qu'elle sait porter. Une base absente ou
    /// <c>anyType</c> n'est donc pas une dérivation.
    /// </remarks>
    private static XsdDerivation Derivation(XmlSchemaType type)
    {
        XmlQualifiedName? baseName = type.BaseXmlSchemaType?.QualifiedName;

        if (baseName is null || baseName.IsEmpty
            || string.Equals(baseName.Name, "anyType", StringComparison.Ordinal)
            || string.Equals(baseName.Name, "anySimpleType", StringComparison.Ordinal))
        {
            return XsdDerivation.None;
        }

        return type.DerivedBy switch
        {
            XmlSchemaDerivationMethod.Extension => XsdDerivation.Extension,
            XmlSchemaDerivationMethod.Restriction => XsdDerivation.Restriction,
            _ => XsdDerivation.None,
        };
    }

    /// <summary>Descend dans une particule, groupes imbriqués compris.</summary>
    private static void Collect(
        XmlSchemaParticle particle, string document, List<XsdParticle> into)
    {
        switch (particle)
        {
            case XmlSchemaElement element:
                into.Add(Element(element, document));

                break;

            case XmlSchemaGroupBase group:
                foreach (XmlSchemaParticle child in group.Items.OfType<XmlSchemaParticle>())
                {
                    Collect(child, document, into);
                }

                break;

            default:
                break;
        }
    }

    private static XsdParticle Element(XmlSchemaElement element, string document) =>
        new(
            element.QualifiedName.Name,
            element.ElementSchemaType?.QualifiedName.ToString() ?? element.SchemaTypeName.ToString(),
            (int)element.MinOccurs,
            element.MaxOccurs == decimal.MaxValue ? null : (int)element.MaxOccurs,
            element.IsNillable,
            At(element, document));

    private static IEnumerable<XsdAttributeUse> Attributes(
        XmlSchemaComplexType type, string document) =>
        type.AttributeUses.Values
            .OfType<XmlSchemaAttribute>()
            .Select(attribute => new XsdAttributeUse(
                attribute.QualifiedName.Name,
                attribute.AttributeSchemaType?.QualifiedName.ToString()
                    ?? attribute.SchemaTypeName.ToString(),
                attribute.Use == XmlSchemaUse.Required,
                At(attribute, document)));

    private static IEnumerable<XsdFacet> Facets(XmlSchemaSimpleType type) =>
        type.Content is XmlSchemaSimpleTypeRestriction restriction
            ? restriction.Facets
                .OfType<XmlSchemaFacet>()
                .Select(facet => new XsdFacet(Name(facet), facet.Value ?? string.Empty))
            : [];

    /// <summary>Le nom d'une facette, tel que XSD l'écrit.</summary>
    private static string Name(XmlSchemaFacet facet) => facet switch
    {
        XmlSchemaMaxLengthFacet => "maxLength",
        XmlSchemaMinLengthFacet => "minLength",
        XmlSchemaLengthFacet => "length",
        XmlSchemaPatternFacet => "pattern",
        XmlSchemaEnumerationFacet => "enumeration",
        XmlSchemaMinInclusiveFacet => "minInclusive",
        XmlSchemaMaxInclusiveFacet => "maxInclusive",
        XmlSchemaMinExclusiveFacet => "minExclusive",
        XmlSchemaMaxExclusiveFacet => "maxExclusive",
        XmlSchemaTotalDigitsFacet => "totalDigits",
        XmlSchemaFractionDigitsFacet => "fractionDigits",
        XmlSchemaWhiteSpaceFacet => "whiteSpace",
        _ => "inconnue",
    };

    /// <summary>Les éléments concrets qui se substituent à une tête dont ce type est le type.</summary>
    /// <remarks>
    /// Le type de la <em>tête</em>, et non celui du membre : la première version comparait le type du
    /// membre, si bien que <c>pieceSignee</c> — dont la tête est <c>piece</c>, de type <c>Document</c> —
    /// se rattachait à son propre type <c>DocumentDetaille</c>. Le compte paraissait juste et l'attribution
    /// était fausse, ce qu'aucune assertion sur un nombre n'aurait montré.
    /// <para>
    /// Relevés parce qu'ils <em>sont</em> le contrat quand la tête est abstraite : lire la tête seule
    /// décrirait un service qui n'accepte rien.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> Substitutes(XmlSchemaComplexType type, XmlSchemaSet set)
    {
        foreach (XmlSchemaElement member in set.GlobalElements.Values.OfType<XmlSchemaElement>())
        {
            if (member.SubstitutionGroup.IsEmpty)
            {
                continue;
            }

            if (set.GlobalElements[member.SubstitutionGroup] is not XmlSchemaElement head)
            {
                continue;
            }

            if (head.ElementSchemaType?.QualifiedName == type.QualifiedName)
            {
                yield return member.QualifiedName.ToString();
            }
        }
    }

    private static bool IsOpen(XmlSchemaComplexType type) =>
        type.AnyAttribute is not null
        || (type.ContentTypeParticle is XmlSchemaGroupBase group
            && group.Items.OfType<XmlSchemaAny>().Any());

    private static WsdlLocation At(XmlSchemaObject element, string document) =>
        new(
            string.IsNullOrEmpty(element.SourceUri) ? document : element.SourceUri,
            "schema",
            element.LineNumber,
            element.LinePosition);
}
