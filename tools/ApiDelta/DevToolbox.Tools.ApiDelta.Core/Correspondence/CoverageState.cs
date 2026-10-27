namespace DevToolbox.Tools.ApiDelta.Core.Correspondence;

/// <summary>Ce que la production fait d'une opération du contrat de service.</summary>
/// <remarks>
/// Un état, et un seul, pour chaque opération. La couverture s'établit avec certitude parce qu'elle repose
/// sur la correspondance confirmée et sur rien d'autre : ce n'est pas une comparaison, c'est le constat
/// d'une présence.
/// <para>
/// Le troisième état existe pour que l'incertitude ne soit pas cachée. Deux candidats également plausibles
/// et aucune confirmation donnent « ambiguë », jamais le premier des deux au hasard de l'ordre de lecture.
/// </para>
/// </remarks>
public enum CoverageState
{
    /// <summary>La production répond à cette opération, par une route confirmée.</summary>
    Covered,

    /// <summary>La production n'y répond nulle part. Un manque, et non une rupture.</summary>
    Absent,

    /// <summary>Plusieurs contreparties possibles, et aucune confirmation.</summary>
    Ambiguous,
}
