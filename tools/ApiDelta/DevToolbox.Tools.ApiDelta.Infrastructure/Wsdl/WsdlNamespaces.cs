namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Les espaces de noms que la lecture d'un contrat WSDL 1.1 reconnaît.</summary>
/// <remarks>
/// Nommés une seule fois. WSDL 2.0 emploie un autre espace de noms pour <c>description</c> : le
/// reconnaître ici est ce qui permet de le refuser en nommant les versions prises en charge, plutôt que
/// d'échouer sur une structure inattendue et de laisser le développeur deviner pourquoi.
/// </remarks>
internal static class WsdlNamespaces
{
    /// <summary>WSDL 1.1.</summary>
    internal const string Wsdl11 = "http://schemas.xmlsoap.org/wsdl/";

    /// <summary>WSDL 2.0, reconnu pour être refusé.</summary>
    internal const string Wsdl20 = "http://www.w3.org/ns/wsdl";

    /// <summary>XSD.</summary>
    internal const string Schema = "http://www.w3.org/2001/XMLSchema";

    /// <summary>La liaison SOAP 1.1.</summary>
    internal const string Soap11 = "http://schemas.xmlsoap.org/wsdl/soap/";

    /// <summary>La liaison SOAP 1.2.</summary>
    internal const string Soap12 = "http://schemas.xmlsoap.org/wsdl/soap12/";
}
