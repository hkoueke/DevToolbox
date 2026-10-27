namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un message, et les parties qu'il porte.</summary>
/// <param name="Name">Son nom qualifié.</param>
/// <param name="Parts">Ses parties, dans l'ordre déclaré.</param>
/// <param name="Location">Où il est déclaré.</param>
public sealed record WsdlMessage(
    string Name, IReadOnlyList<WsdlPart> Parts, WsdlLocation Location);
