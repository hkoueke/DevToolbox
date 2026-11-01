namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Décrit une déclaration wsdl:import sans effectuer de résolution réseau.</summary>
/// <param name="Namespace">Namespace déclaré par l'import.</param>
/// <param name="Location">Emplacement de la ressource importée.</param>
public sealed record WsdlImport(string? Namespace, string? Location)
{
    /// <summary>Indique si l'import fournit les informations nécessaires à sa résolution.</summary>
    public bool HasRequiredAttributes =>
        !string.IsNullOrWhiteSpace(Namespace)
        && !string.IsNullOrWhiteSpace(Location);
}
