namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Ce qu'un document du graphe est.</summary>
public enum WsdlDocumentKind
{
    /// <summary>Un document <c>wsdl:definitions</c>.</summary>
    Definitions,

    /// <summary>Un schéma XSD, importé ou inclus.</summary>
    Schema,
}
