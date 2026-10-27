namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un service, et les accès qu'il expose.</summary>
/// <param name="Name">Son nom.</param>
/// <param name="Ports">Ses accès : le nom de la liaison, et l'emplacement déclaré.</param>
/// <param name="Location">Où il est déclaré.</param>
public sealed record WsdlService(
    string Name, IReadOnlyList<WsdlPort> Ports, WsdlLocation Location);
