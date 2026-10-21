namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce qu'une règle rapporte à son propre sujet.</summary>
/// <remarks>
/// C'est ici que tient la promesse de ne pas annoncer propre ce qui n'a pas été regardé. Une règle qui
/// n'a pas pu s'exécuter le dit, avec sa raison, et le rapport ne peut plus se présenter comme complet.
/// </remarks>
public enum RuleState
{
    /// <summary>La règle s'est exécutée sur tout ce qu'elle devait voir.</summary>
    Evaluated,

    /// <summary>La règle n'a pas pu s'exécuter, et le rapport n'est donc pas complet.</summary>
    NotEvaluated,

    /// <summary>La règle s'est exécutée mais a rendu moins que ce qu'elle a trouvé.</summary>
    Truncated,

    /// <summary>Le jeu de règles l'a désactivée.</summary>
    Disabled,

    /// <summary>Ses constats ont été mis en sourdine.</summary>
    Suppressed,
}
