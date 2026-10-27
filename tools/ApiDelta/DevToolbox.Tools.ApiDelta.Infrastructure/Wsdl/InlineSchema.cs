using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Un schéma écrit à l'intérieur de <c>wsdl:types</c>.</summary>
/// <param name="TargetNamespace">Son espace de noms, absent pour une inclusion caméléon.</param>
/// <param name="Element">L'élément <c>xs:schema</c> lui-même.</param>
/// <param name="Location">Où il se trouve.</param>
internal sealed record InlineSchema(
    string? TargetNamespace, XElement Element, WsdlLocation Location);
