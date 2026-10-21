namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>
/// Les identifiants stables des sortes de changements.
/// </summary>
/// <remarks>
/// Stables parce qu'ils atteignent les rapports, et qu'un lecteur qui filtre sur l'un d'eux doit obtenir
/// la même chose d'une version à l'autre.
/// </remarks>
public static class ChangeIds
{
    /// <summary>Un chemin a été ajouté.</summary>
    public const string PathAdded = "PATH-ADDED";

    /// <summary>Un chemin a été retiré.</summary>
    public const string PathRemoved = "PATH-REMOVED";

    /// <summary>Une opération a été ajoutée.</summary>
    public const string OperationAdded = "OPERATION-ADDED";

    /// <summary>Une opération a été retirée.</summary>
    public const string OperationRemoved = "OPERATION-REMOVED";

    /// <summary>Un paramètre a été ajouté.</summary>
    public const string ParameterAdded = "PARAMETER-ADDED";

    /// <summary>Un paramètre a été retiré.</summary>
    public const string ParameterRemoved = "PARAMETER-REMOVED";

    /// <summary>Un paramètre est devenu obligatoire.</summary>
    public const string ParameterBecameRequired = "PARAMETER-REQUIRED-ADDED";

    /// <summary>Un paramètre est devenu facultatif.</summary>
    public const string ParameterBecameOptional = "PARAMETER-REQUIRED-REMOVED";

    /// <summary>Une réponse a été ajoutée.</summary>
    public const string ResponseAdded = "RESPONSE-ADDED";

    /// <summary>Une réponse a été retirée.</summary>
    public const string ResponseRemoved = "RESPONSE-REMOVED";

    /// <summary>Une valeur d'énumération a été ajoutée.</summary>
    public const string EnumValueAdded = "ENUM-VALUE-ADDED";

    /// <summary>Une valeur d'énumération a été retirée.</summary>
    public const string EnumValueRemoved = "ENUM-VALUE-REMOVED";

    /// <summary>Une contrainte a été resserrée.</summary>
    public const string ConstraintNarrowed = "CONSTRAINT-NARROWED";

    /// <summary>Une contrainte a été élargie.</summary>
    public const string ConstraintWidened = "CONSTRAINT-WIDENED";

    /// <summary>Une propriété est devenue obligatoire.</summary>
    public const string PropertyBecameRequired = "PROPERTY-REQUIRED-ADDED";

    /// <summary>Une propriété a cessé d'être obligatoire.</summary>
    public const string PropertyBecameOptional = "PROPERTY-REQUIRED-REMOVED";

    /// <summary>Un type a changé.</summary>
    public const string TypeChanged = "TYPE-CHANGED";

    /// <summary>Un format a changé.</summary>
    public const string FormatChanged = "FORMAT-CHANGED";

    /// <summary>Une exigence de sécurité a changé.</summary>
    public const string SecurityChanged = "SECURITY-CHANGED";

    /// <summary>De la documentation a changé.</summary>
    public const string DocumentationChanged = "DOCUMENTATION-CHANGED";

    /// <summary>Une extension de fournisseur a changé.</summary>
    public const string ExtensionChanged = "EXTENSION-CHANGED";

    /// <summary>Un champ a été ajouté.</summary>
    public const string FieldAdded = "FIELD-ADDED";

    /// <summary>Un champ a été retiré.</summary>
    public const string FieldRemoved = "FIELD-REMOVED";

    /// <summary>La valeur d'un champ a changé.</summary>
    public const string FieldChanged = "FIELD-CHANGED";
}
