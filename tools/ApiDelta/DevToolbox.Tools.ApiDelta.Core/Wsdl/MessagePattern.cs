namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Le schéma d'échange d'une opération WSDL 1.1.</summary>
/// <remarks>
/// Les quatre, et seulement eux. L'ordre des éléments <c>input</c> et <c>output</c> dans le type de port
/// les distingue : entrée puis sortie, entrée seule, sortie puis entrée, sortie seule. Supposer la
/// requête-réponse partout — ce que fait la plupart de l'outillage — ferait projeter une réponse à une
/// opération qui n'en rend aucune.
/// </remarks>
public enum MessagePattern
{
    /// <summary>Entrée puis sortie.</summary>
    RequestResponse,

    /// <summary>Entrée seule : rien n'est rendu, donc rien n'est à projeter en réponse.</summary>
    OneWay,

    /// <summary>Sortie puis entrée : le service interroge, l'appelant répond.</summary>
    SolicitResponse,

    /// <summary>Sortie seule.</summary>
    Notification,
}
