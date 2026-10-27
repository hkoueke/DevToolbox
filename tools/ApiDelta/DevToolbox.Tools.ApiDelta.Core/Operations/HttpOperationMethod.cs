namespace DevToolbox.Tools.ApiDelta.Core.Operations;

/// <summary>Une méthode d'opération OpenAPI.</summary>
/// <remarks>
/// Les huit, et seulement elles : ce qui n'est pas une méthode dans un élément de chemin est un champ,
/// par exemple <c>parameters</c> ou <c>summary</c>, et les confondre produirait des opérations fantômes.
/// </remarks>
public enum HttpOperationMethod
{
    /// <summary>GET.</summary>
    Get,

    /// <summary>PUT.</summary>
    Put,

    /// <summary>POST.</summary>
    Post,

    /// <summary>DELETE.</summary>
    Delete,

    /// <summary>OPTIONS.</summary>
    Options,

    /// <summary>HEAD.</summary>
    Head,

    /// <summary>PATCH.</summary>
    Patch,

    /// <summary>TRACE.</summary>
    Trace,
}
