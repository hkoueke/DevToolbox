namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>L'emploi d'un attribut par un type complexe — une « attribute use », dans le vocabulaire XSD.</summary>
/// <param name="Name">Son nom local.</param>
/// <param name="TypeName">Le nom qualifié de son type.</param>
/// <param name="Required">Il est exigé.</param>
/// <param name="Location">Où il est déclaré.</param>
/// <remarks>
/// Porté comme forme de lecture, et jamais projeté : OpenAPI n'a pas d'attributs. Un attribut est donc
/// toujours un trou — et le plus fréquent de tous, ce qui est la raison pour laquelle les trous se
/// regroupent par motif au lieu de se compter un par un.
/// </remarks>
public sealed record XsdAttributeUse(
    string Name, string TypeName, bool Required, WsdlLocation Location);
