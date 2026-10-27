namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Une partie de message : ce qu'elle nomme, et par quoi.</summary>
/// <param name="Name">Son nom.</param>
/// <param name="ElementName">L'élément qu'elle nomme, pour un style <c>document</c>.</param>
/// <param name="TypeName">Le type qu'elle nomme, pour un style <c>rpc</c>.</param>
/// <param name="Location">Où elle est déclarée.</param>
/// <remarks>
/// Une partie nomme un élément <em>ou</em> un type, jamais les deux et jamais aucun : les deux écarts sont
/// des erreurs de contrat, et les relever demande de porter les deux champs séparément plutôt qu'un seul
/// champ « ce qu'elle nomme ».
/// </remarks>
public sealed record WsdlPart(
    string Name, string? ElementName, string? TypeName, WsdlLocation Location)
{
    /// <summary>Indique que la partie est bien formée : elle nomme exactement l'un des deux.</summary>
    public bool IsWellFormed =>
        string.IsNullOrEmpty(ElementName) != string.IsNullOrEmpty(TypeName);
}
