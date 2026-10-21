namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>Comment deux membres d'une collection se reconnaissent d'un document à l'autre.</summary>
/// <remarks>
/// C'est ce qui distingue « ce paramètre a changé » de « ce paramètre a disparu et un autre est apparu ».
/// Un rang ne fait pas une identité : réordonner des paramètres n'est pas les remplacer.
/// </remarks>
public enum IdentityRule
{
    /// <summary>Par la clé de l'association.</summary>
    ByKey,

    /// <summary>Par le couple nom et emplacement, comme un paramètre OpenAPI.</summary>
    ByNameAndLocation,

    /// <summary>Par le rang, faute de mieux.</summary>
    ByIndex,

    /// <summary>Par la valeur canonique de l'élément, pour un ensemble.</summary>
    ByValue,
}
