namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>À quel point l'outil répond de sa classification.</summary>
public enum Confidence
{
    /// <summary>La spécification le dit.</summary>
    Certain,

    /// <summary>La règle vaut dans tous les cas usuels, et un cas particulier pourrait la démentir.</summary>
    Probable,

    /// <summary>Le classement repose sur une heuristique, et mérite d'être relu.</summary>
    Uncertain,
}
