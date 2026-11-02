using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Décrit le détail déclaré d'un SOAP Fault sans inventer son statut HTTP.</summary>
internal static class SoapFaultSchemas
{
    internal static SchemaDraft Envelope(SchemaDraft detail, XNamespace soap)
    {
        string space = SoapDocumentExporter.EnvelopeNamespace(soap);
        SchemaDraft fault = SoapXmlSchemas.Object();
        SoapXmlSchemas.Xml(fault, "Fault", space);
        // The SOAP protocol controls the other Fault fields. Only detail is constrained by WSDL.
        fault.Set("additionalProperties", true);
        fault.Set("description", "SOAP Fault : seul le détail métier déclaré par le WSDL est contraint. "
            + "Les champs de protocole (Code/Reason ou faultcode/faultstring) restent définis par SOAP.");
        string detailName = string.Equals(soap.NamespaceName, WsdlNamespaces.Soap11, StringComparison.Ordinal) ? "detail" : "Detail";
        SoapXmlSchemas.Xml(detail, detailName, string.Equals(soap.NamespaceName, WsdlNamespaces.Soap11, StringComparison.Ordinal) ? string.Empty : space);
        fault.Section("properties").Set(detailName, detail);
        fault.Append("required", detailName);
        SchemaDraft body = SoapXmlSchemas.Object();
        SoapXmlSchemas.Xml(body, "Body", space);
        body.Section("properties").Set("Fault", fault);
        body.Append("required", "Fault");
        SchemaDraft envelope = SoapXmlSchemas.Object();
        SoapXmlSchemas.Xml(envelope, "Envelope", space);
        envelope.Section("properties").Set("Body", body);
        envelope.Append("required", "Body");
        return envelope;
    }
}
