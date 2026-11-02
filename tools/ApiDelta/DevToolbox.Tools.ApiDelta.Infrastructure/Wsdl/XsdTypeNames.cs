using System.Globalization;
using System.Xml;
using System.Xml.Schema;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Nomme les types en ligne sans modifier le jeu XML compilé ni confondre éléments et types.</summary>
internal sealed class XsdTypeNames
{
    private readonly Dictionary<XmlSchemaType, XmlQualifiedName> _names = [];
    private readonly Dictionary<string, XmlSchemaType> _types = new(StringComparer.Ordinal);
    private readonly Queue<XmlSchemaType> _pending = new();

    internal XsdTypeNames(XmlSchemaSet set)
    {
        foreach (XmlSchemaType type in set.GlobalTypes.Values.OfType<XmlSchemaType>()
            .Where(type => !type.QualifiedName.IsEmpty && !string.Equals(type.QualifiedName.Namespace, WsdlNamespaces.Schema, StringComparison.Ordinal))
            .OrderBy(type => type.QualifiedName.ToString(), StringComparer.Ordinal))
        {
            Register(type, type.QualifiedName);
        }

        foreach (XmlSchemaElement element in set.GlobalElements.Values.OfType<XmlSchemaElement>()
            .OrderBy(element => element.QualifiedName.ToString(), StringComparer.Ordinal))
        {
            Inline(element.ElementSchemaType, element.QualifiedName.Namespace, "__inline_" + element.QualifiedName.Name);
        }

        while (_pending.TryDequeue(out XmlSchemaType? type))
        {
            if (type is not XmlSchemaComplexType complex)
            {
                continue;
            }

            XmlQualifiedName parent = _names[type];
            foreach (XmlSchemaElement element in Elements(complex.ContentTypeParticle))
            {
                Inline(element.ElementSchemaType, parent.Namespace, parent.Name + "__" + element.QualifiedName.Name);
            }
        }
    }

    internal IReadOnlyDictionary<string, XmlSchemaType> Types => _types;

    internal string NameOf(XmlSchemaType? type)
    {
        if (type is null)
        {
            return string.Empty;
        }

        return type.QualifiedName.IsEmpty ? _names.GetValueOrDefault(type)?.ToString() ?? string.Empty : type.QualifiedName.ToString();
    }

    private void Inline(XmlSchemaType? type, string space, string proposed)
    {
        if (type is null || !type.QualifiedName.IsEmpty || _names.ContainsKey(type))
        {
            return;
        }

        XmlQualifiedName name = new(proposed, space);
        int suffix = 2;
        while (_types.ContainsKey(name.ToString()))
        {
            name = new XmlQualifiedName(proposed + "_" + suffix.ToString(CultureInfo.InvariantCulture), space);
            suffix++;
        }

        Register(type, name);
    }

    private void Register(XmlSchemaType type, XmlQualifiedName name)
    {
        _names[type] = name;
        _types[name.ToString()] = type;
        _pending.Enqueue(type);
    }

    private static IEnumerable<XmlSchemaElement> Elements(XmlSchemaParticle particle)
    {
        if (particle is XmlSchemaElement element)
        {
            yield return element;
        }
        else if (particle is XmlSchemaGroupBase group)
        {
            foreach (XmlSchemaParticle child in group.Items.OfType<XmlSchemaParticle>())
            {
                foreach (XmlSchemaElement nested in Elements(child))
                {
                    yield return nested;
                }
            }
        }
    }
}
