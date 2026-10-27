namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>La version de SOAP qu'une liaison emploie.</summary>
/// <remarks>
/// Les deux versions sont projetées de la même façon : ce qui change entre elles — l'enveloppe, le code
/// de défaut, l'en-tête <c>SOAPAction</c> — ne franchit pas la projection. La version est néanmoins portée,
/// parce qu'une liaison qui n'est ni l'une ni l'autre est une liaison dont l'outil ne sait rien dire, et
/// qu'il doit le dire.
/// </remarks>
public enum SoapVersion
{
    /// <summary>Aucune liaison SOAP : HTTP nu, ou une extension que l'outil ne comprend pas.</summary>
    None,

    /// <summary>SOAP 1.1.</summary>
    Soap11,

    /// <summary>SOAP 1.2.</summary>
    Soap12,
}
