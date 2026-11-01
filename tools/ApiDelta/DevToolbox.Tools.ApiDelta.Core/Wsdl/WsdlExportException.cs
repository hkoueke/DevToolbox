namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Refus d'export accompagné d'un motif sûr à afficher, sans contenu d'entrée.</summary>
public sealed class WsdlExportException : InvalidOperationException
{
    /// <summary>Crée un refus à partir d'un code de construction non représentable.</summary>
    public WsdlExportException(string reason)
        : base("Export WSDL refusé : " + Explain(reason) + " (" + reason + ").")
    {
    }

    private static string Explain(string reason) => reason switch
    {
        "SOAP_ACTION_UNSAFE" => "la SOAPAction contient une autorité, des données sensibles ou une forme d'URI non exportable",
        "XSD_BINARY_FACET_UNSUPPORTED" => "les facettes binaires portent sur les octets décodés et ne peuvent pas être traduites en contraintes de chaîne",
        "XSD_ATTRIBUTES_UNSUPPORTED" => "les attributs XML ne sont pas pris en charge",
        "XSD_MIXED_CONTENT_UNSUPPORTED" => "le contenu XML mixte ne peut pas être représenté",
        "XSD_SUBSTITUTION_GROUP_OR_ABSTRACT_ELEMENT_UNSUPPORTED" => "les groupes de substitution et éléments abstraits ne sont pas pris en charge",
        "SOAP_RPC_UNSUPPORTED" => "le style SOAP RPC ne peut pas être projeté",
        "SOAP_ENCODED_OR_MISSING_LITERAL_BODY_UNSUPPORTED" => "un corps SOAP literal explicite est requis ; encoded est refusé",
        "SOAP_ENDPOINT_UNSAFE_OR_AMBIGUOUS" or "SOAP_ENDPOINT_PATH_UNSAFE" => "l'adresse SOAP est ambiguë ou contient des éléments non exportables",
        "WSDL_XSD_GRAPH_UNRESOLVED_OR_INVALID" => "un import ou une définition WSDL/XSD est invalide, inaccessible ou hors du périmètre autorisé",
        "QNAME_UNRESOLVED" or "QNAME_INVALID" or "QNAME_PREFIX_UNDECLARED" => "une référence qualifiée est invalide ou ne désigne aucune déclaration du graphe",
        "XML_DTD_OR_PARSE_INVALID" => "le XML est invalide ou porte une DTD interdite",
        "XSD_PROJECTION_DEPTH_LIMIT" or "DOCUMENT_DEPTH_LIMIT" or "DOCUMENT_BYTE_LIMIT" => "une limite de lecture ou de projection a été atteinte",
        _ => "une construction du contrat ne peut pas être représentée sans perte ; aucun YAML n'est publié",
    };
}
