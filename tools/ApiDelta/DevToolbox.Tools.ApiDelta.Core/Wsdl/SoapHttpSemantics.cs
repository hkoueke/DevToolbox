namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Les paramètres HTTP propres à chaque version de SOAP.</summary>
/// <remarks>
/// Ces informations décrivent la liaison SOAP d'origine. Elles ne modifient pas
/// la projection REST synthétique utilisée par le moteur de comparaison.
/// </remarks>
public static class SoapHttpSemantics
{
    /// <summary>Retourne le type de contenu HTTP du message SOAP.</summary>
    /// <param name="version">La version de la liaison SOAP.</param>
    /// <returns>Le type MIME, ou rien pour une liaison non SOAP.</returns>
    public static string? MediaType(SoapVersion version) => version switch
    {
        SoapVersion.Soap11 => "text/xml",
        SoapVersion.Soap12 => "application/soap+xml",
        _ => null,
    };

    /// <summary>Indique si SOAPAction est un en-tête HTTP indépendant.</summary>
    /// <param name="version">La version de la liaison SOAP.</param>
    /// <returns>Vrai uniquement pour SOAP 1.1.</returns>
    public static bool UsesSoapActionHeader(SoapVersion version) =>
        version == SoapVersion.Soap11;
}
