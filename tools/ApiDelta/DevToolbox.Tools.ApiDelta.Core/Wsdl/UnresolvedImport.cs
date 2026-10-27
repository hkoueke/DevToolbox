namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un import que la politique n'a pas laissé résoudre.</summary>
/// <param name="Namespace">L'espace de noms visé.</param>
/// <param name="SchemaLocation">L'emplacement tel que le document l'écrit.</param>
/// <param name="Wanted">Où l'import est écrit : le document qui le voulait, et sa position.</param>
/// <param name="Why">Pourquoi il n'a pas été suivi, dit à un lecteur.</param>
/// <remarks>
/// Nommer les deux côtés est ce qui rend le refus exploitable : savoir qu'un schéma manque ne dit pas où
/// aller le chercher, et savoir quel import le voulait le dit.
/// </remarks>
public sealed record UnresolvedImport(
    string Namespace, string SchemaLocation, WsdlLocation Wanted, string Why);
