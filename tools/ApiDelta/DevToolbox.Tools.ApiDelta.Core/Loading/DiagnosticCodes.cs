namespace DevToolbox.Tools.ApiDelta.Core.Loading;

/// <summary>
/// Les identifiants stables des diagnostics de lecture.
/// </summary>
/// <remarks>
/// Stables parce qu'ils atteignent les rapports : un lecteur qui cherche pourquoi une exécution a échoué
/// doit retrouver le même mot d'une version à l'autre.
/// </remarks>
public static class DiagnosticCodes
{
    /// <summary>La description n'est ni du JSON ni du YAML analysable.</summary>
    public const string ParseFailed = "LOAD-PARSE-FAILED";

    /// <summary>Une clé d'association apparaît deux fois.</summary>
    public const string DuplicateKey = "LOAD-DUPLICATE-KEY";

    /// <summary>La version d'OpenAPI déclarée n'est pas prise en charge.</summary>
    public const string UnsupportedVersion = "LOAD-UNSUPPORTED-VERSION";

    /// <summary>La version d'OpenAPI n'est pas déclarée du tout.</summary>
    public const string MissingVersion = "LOAD-MISSING-VERSION";

    /// <summary>La description viole la structure de la version qu'elle déclare.</summary>
    public const string StructuralViolation = "LOAD-STRUCTURE";

    /// <summary>Une référence ne se résout pas.</summary>
    public const string UnresolvedReference = "LOAD-UNRESOLVED-REF";

    /// <summary>Une référence est interdite par la politique en vigueur.</summary>
    public const string DisallowedReference = "LOAD-DISALLOWED-REF";

    /// <summary>Un cycle traverse des composants pour lesquels il n'est pas légal.</summary>
    public const string IllegalCycle = "LOAD-ILLEGAL-CYCLE";

    /// <summary>Une limite de taille, de profondeur ou de nombre de références a été atteinte.</summary>
    public const string LimitReached = "LOAD-LIMIT";

    /// <summary>Un scalaire dont la résolution n'est pas univoque.</summary>
    public const string AmbiguousScalar = "LOAD-AMBIGUOUS-SCALAR";

    /// <summary>Du texte encodé là où du texte décodé est attendu.</summary>
    public const string SuspiciousEncoding = "LOAD-SUSPICIOUS-ENCODING";

    /// <summary>Une clé d'association qui n'est pas une chaîne.</summary>
    public const string NonStringKey = "LOAD-NON-STRING-KEY";
}
