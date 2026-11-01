using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Projection XML des seuls types dont la forme est représentable en OpenAPI 3.</summary>
internal sealed class SoapXmlSchemas(XmlSchemaSet schemas, SchemaDraft components)
{
    private readonly Dictionary<XmlSchemaType, string> _names = [];
    private int _depth;

    internal SchemaDraft Element(XmlQualifiedName name)
    {
        if (schemas.GlobalElements[name] is not XmlSchemaElement element)
        {
            throw new WsdlExportException("XSD_ELEMENT_UNRESOLVED");
        }

        return Element(element);
    }

    private SchemaDraft Element(XmlSchemaElement element)
    {
        if (element.IsAbstract || !element.SubstitutionGroup.IsEmpty
            || schemas.GlobalElements.Values.OfType<XmlSchemaElement>().Any(candidate =>
                candidate.SubstitutionGroup == element.QualifiedName))
        {
            throw new WsdlExportException("XSD_SUBSTITUTION_GROUP_OR_ABSTRACT_ELEMENT_UNSUPPORTED");
        }

        if (element.Constraints.Count > 0 || element.FixedValue is not null
            || element.DefaultValue is not null)
        {
            throw new WsdlExportException("XSD_IDENTITY_DEFAULT_OR_FIXED_UNSUPPORTED");
        }

        SchemaDraft draft = new();
        draft.Append("allOf", Type(element.ElementSchemaType
            ?? throw new WsdlExportException("XSD_TYPE_UNRESOLVED")));
        Xml(draft, element.QualifiedName.Name, element.QualifiedName.Namespace);
        if (element.IsNillable)
        {
            // OAS nullable describes null values, not the required xsi:nil attribute.
            throw new WsdlExportException("XSD_NILLABLE_UNSUPPORTED");
        }

        return draft;
    }

    private SchemaDraft Type(XmlSchemaType type)
    {
        if (string.Equals(type.QualifiedName.Namespace, XmlSchema.Namespace, StringComparison.Ordinal))
        {
            return Primitive(type.Datatype?.TypeCode ?? XmlTypeCode.None);
        }

        if (_names.TryGetValue(type, out string? existing))
        {
            return Reference(existing);
        }

        string key = "XmlType" + (_names.Count + 1).ToString(CultureInfo.InvariantCulture);
        _names.Add(type, key);
        if (++_depth > 64)
        {
            throw new WsdlExportException("XSD_PROJECTION_DEPTH_LIMIT");
        }

        SchemaDraft draft = type switch
        {
            XmlSchemaComplexType complex => Complex(complex),
            XmlSchemaSimpleType simple => Simple(simple),
            _ => throw new WsdlExportException("XSD_TYPE_UNSUPPORTED"),
        };
        _depth--;
        components.Set(key, draft);
        return Reference(key);
    }

    private SchemaDraft Complex(XmlSchemaComplexType type)
    {
        if (type.IsMixed)
        {
            throw new WsdlExportException("XSD_MIXED_CONTENT_UNSUPPORTED");
        }

        if (type.AttributeUses.Count > 0 || type.AnyAttribute is not null)
        {
            throw new WsdlExportException("XSD_ATTRIBUTES_UNSUPPORTED");
        }

        if (type.IsAbstract || type.ContentModel is XmlSchemaSimpleContent)
        {
            throw new WsdlExportException("XSD_ABSTRACT_OR_SIMPLE_CONTENT_UNSUPPORTED");
        }

        SchemaDraft draft = Object();
        AddParticle(draft, type.ContentTypeParticle);
        return draft;
    }

    private void AddParticle(SchemaDraft draft, XmlSchemaParticle particle)
    {
        switch (particle)
        {
            case XmlSchemaElement element:
                AddElement(draft, element);
                break;
            case XmlSchemaSequence sequence when sequence.MinOccurs == 1 && sequence.MaxOccurs == 1:
                foreach (XmlSchemaParticle child in sequence.Items.OfType<XmlSchemaParticle>())
                {
                    AddParticle(draft, child);
                }

                break;
            case XmlSchemaAll all when all.MinOccurs == 1 && all.MaxOccurs == 1:
                foreach (XmlSchemaParticle child in all.Items.OfType<XmlSchemaParticle>())
                {
                    AddParticle(draft, child);
                }

                break;
            case XmlSchemaSequence or XmlSchemaAll or XmlSchemaChoice or XmlSchemaAny:
                throw new WsdlExportException("XSD_CHOICE_WILDCARD_OR_GROUP_CARDINALITY_UNSUPPORTED");
            default:
                // XmlSchemaParticle.Empty has no public type.
                if (particle.MinOccurs != 0 || particle.MaxOccurs != 0)
                {
                    throw new WsdlExportException("XSD_PARTICLE_UNSUPPORTED");
                }

                break;
        }
    }

    private void AddElement(SchemaDraft draft, XmlSchemaElement element)
    {
        string name = element.QualifiedName.Name;
        if (draft.Section("properties").Get(name) is not null)
        {
            throw new WsdlExportException("XSD_PROPERTY_NAME_COLLISION");
        }

        SchemaDraft value = Element(element);
        if (element.MaxOccurs != 1)
        {
            SchemaDraft array = new();
            array.Set("type", "array");
            array.Set("items", value);
            array.Set("minItems", element.MinOccurs);
            if (element.MaxOccurs != decimal.MaxValue)
            {
                array.Set("maxItems", element.MaxOccurs);
            }

            Xml(array, name, element.QualifiedName.Namespace);
            array.Section("xml").Set("wrapped", false);
            value = array;
        }

        draft.Section("properties").Set(name, value);
        if (element.MinOccurs > 0)
        {
            draft.Append("required", name);
        }
    }

    private SchemaDraft Simple(XmlSchemaSimpleType type)
    {
        if (type.Content is not XmlSchemaSimpleTypeRestriction restriction
            || restriction.BaseType is not null)
        {
            throw new WsdlExportException("XSD_LIST_UNION_OR_INLINE_RESTRICTION_UNSUPPORTED");
        }

        SchemaDraft draft = Primitive(type.Datatype?.TypeCode ?? XmlTypeCode.None);
        if (type.BaseXmlSchemaType is { QualifiedName.Namespace: not XmlSchema.Namespace } parent)
        {
            draft.Append("allOf", Type(parent));
        }

        foreach (XmlSchemaFacet facet in restriction.Facets.OfType<XmlSchemaFacet>())
        {
            if (facet is XmlSchemaEnumerationFacet)
            {
                if (draft.Get("type") is not "string")
                {
                    throw new WsdlExportException("XSD_NON_STRING_ENUM_UNSUPPORTED");
                }

                draft.Append("enum", facet.Value);
            }
            else if (facet is XmlSchemaMinLengthFacet or XmlSchemaMaxLengthFacet or XmlSchemaLengthFacet
                && draft.Get("type") is "string")
            {
                int length = int.Parse(facet.Value!, CultureInfo.InvariantCulture);
                if (facet is XmlSchemaMinLengthFacet or XmlSchemaLengthFacet)
                {
                    draft.Set("minLength", length);
                }

                if (facet is XmlSchemaMaxLengthFacet or XmlSchemaLengthFacet)
                {
                    draft.Set("maxLength", length);
                }
            }
            else
            {
                throw new WsdlExportException("XSD_FACET_UNSUPPORTED");
            }
        }

        return draft;
    }

    private static SchemaDraft Primitive(XmlTypeCode code)
    {
        SchemaDraft draft = new();
        switch (code)
        {
            case XmlTypeCode.String:
                draft.Set("type", "string");
                break;
            case XmlTypeCode.Boolean:
                draft.Set("type", "boolean");
                break;
            case XmlTypeCode.Int:
                draft.Set("type", "integer"); draft.Set("format", "int32");
                break;
            case XmlTypeCode.Long:
                draft.Set("type", "integer"); draft.Set("format", "int64");
                break;
            case XmlTypeCode.Decimal:
                draft.Set("type", "number");
                break;
            case XmlTypeCode.Base64Binary:
                draft.Set("type", "string"); draft.Set("format", "byte");
                break;
            default:
                throw new WsdlExportException("XSD_PRIMITIVE_UNSUPPORTED");
        }

        return draft;
    }

    internal static SchemaDraft Object()
    {
        SchemaDraft draft = new();
        draft.Set("type", "object");
        draft.Set("additionalProperties", false);
        return draft;
    }

    internal static void Xml(SchemaDraft draft, string name, string space)
    {
        draft.Section("xml").Set("name", name);
        if (space.Length > 0)
        {
            draft.Section("xml").Set("namespace", space);
            draft.Section("xml").Set("prefix", "ns");
        }
    }

    private static SchemaDraft Reference(string key)
    {
        SchemaDraft draft = new();
        draft.Set("$ref", "#/components/schemas/" + key);
        return draft;
    }
}
