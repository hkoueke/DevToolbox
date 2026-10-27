namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Une opération : l'unité qu'une correspondance apparie et qu'une sélection retient.</summary>
/// <param name="Name">Son nom, tel que le type de port l'écrit.</param>
/// <param name="Pattern">Son schéma d'échange.</param>
/// <param name="Input">Son message d'entrée, absent pour une notification.</param>
/// <param name="Output">Son message de sortie, absent pour un sens unique.</param>
/// <param name="Faults">Ses défauts : une correspondance avec les codes de statut, jamais une équivalence.</param>
/// <param name="Binding">Ce que la liaison lui impose.</param>
/// <param name="Documentation">Sa documentation, quand le contrat en porte.</param>
/// <param name="Location">Où elle est déclarée.</param>
/// <remarks>
/// L'entrée et la sortie sont toutes deux facultatives, et c'est ce qui distingue les quatre schémas
/// d'échange de WSDL 1.1. Supposer une réponse partout ferait projeter un corps de réponse à une opération
/// à sens unique, qui n'en rend aucun.
/// </remarks>
public sealed record WsdlOperation(
    string Name,
    MessagePattern Pattern,
    WsdlMessage? Input,
    WsdlMessage? Output,
    IReadOnlyList<WsdlMessage> Faults,
    WsdlBinding? Binding,
    string? Documentation,
    WsdlLocation Location)
{
    /// <summary>Indique que l'opération rend quelque chose.</summary>
    public bool HasResponse => Output is not null;

    /// <summary>Indique qu'aucune liaison n'expose cette opération.</summary>
    /// <remarks>
    /// Une opération déclarée dans un type de port et liée par rien n'est joignable par aucun appelant.
    /// Ce n'est pas une subtilité de style : c'est une erreur de contrat, et la correspondance ne devrait
    /// pas l'apparier.
    /// </remarks>
    public bool IsUnreachable => Binding is null;
}
