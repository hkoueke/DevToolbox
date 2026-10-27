using System.Xml.Linq;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Un schéma lu depuis le disque, et le document d'où il vient.</summary>
/// <param name="Document">Le chemin du fichier.</param>
/// <param name="Element">Son élément <c>xs:schema</c>.</param>
internal sealed record ReadSchema(string Document, XElement Element);
