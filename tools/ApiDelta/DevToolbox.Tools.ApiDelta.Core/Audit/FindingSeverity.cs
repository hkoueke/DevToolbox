namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Combien une équipe se soucie d'un constat.</summary>
/// <remarks>
/// À ne pas confondre avec la classe de sûreté : la gravité dit l'importance, la sûreté dit ce que l'outil
/// peut prouver. Un jeu de règles peut changer la première ; il ne peut pas toucher la seconde, car seule
/// la seconde décide si quelque chose peut être appliqué.
/// </remarks>
public enum FindingSeverity
{
    /// <summary>Un défaut.</summary>
    Error,

    /// <summary>Quelque chose qui mérite une correction.</summary>
    Warning,

    /// <summary>Une observation.</summary>
    Information,

    /// <summary>Une occasion de réduction ou de simplification.</summary>
    Opportunity,
}
