using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Décrit la représentation HTTP d'une opération SOAP pour l'export OpenAPI.</summary>
/// <remarks>
/// Une opération SOAP utilise HTTP POST ; sa charge utile reste une enveloppe XML,
/// et ne doit pas être confondue avec la projection REST synthétique du comparateur.
/// </remarks>
public static class SoapOpenApiOperation
{
    /// <summary>La méthode HTTP utilisée pour transmettre un message SOAP.</summary>
    public const string HttpMethod = "post";

    /// <summary>Retourne le type MIME du corps d'une opération SOAP.</summary>
    /// <param name="version">La version de SOAP.</param>
    /// <returns>Le type MIME, ou rien si la liaison n'est pas SOAP.</returns>
    public static string? RequestMediaType(SoapVersion version) => version switch
    {
        SoapVersion.Soap11 => "text/xml",
        SoapVersion.Soap12 => "application/soap+xml",
        _ => null,
    };

    /// <summary>Retourne le namespace de l'enveloppe SOAP.</summary>
    /// <param name="version">La version de SOAP.</param>
    /// <returns>Le namespace XML de l'enveloppe, ou rien si non SOAP.</returns>
    public static string? EnvelopeNamespace(SoapVersion version) => version switch
    {
        SoapVersion.Soap11 => "http://schemas.xmlsoap.org/soap/envelope/",
        SoapVersion.Soap12 => "http://www.w3.org/2003/05/soap-envelope",
        _ => null,
    };
}
