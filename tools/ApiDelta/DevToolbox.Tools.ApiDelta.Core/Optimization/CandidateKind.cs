namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>La sorte de chose qu'un groupe de répétition rassemble.</summary>
/// <remarks>
/// Elle décide du remède : un paramètre s'extrait dans une famille de composants, une énumération ne
/// s'extrait pas du tout. C'est pourquoi la sorte est portée par le groupe et non déduite à l'affichage.
/// </remarks>
public enum CandidateKind
{
    /// <summary>Une sorte que l'outil n'a pas rangée.</summary>
    Other,

    /// <summary>Un paramètre.</summary>
    Parameter,

    /// <summary>Une réponse.</summary>
    Response,

    /// <summary>Un corps de requête.</summary>
    RequestBody,

    /// <summary>Un schéma.</summary>
    Schema,

    /// <summary>Un en-tête de réponse.</summary>
    Header,

    /// <summary>Un exemple.</summary>
    Example,

    /// <summary>Un jeu de valeurs énumérées.</summary>
    Enumeration,
}
