namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce que l'outil peut prouver au sujet d'un remède.</summary>
public enum SafetyClass
{
    /// <summary>
    /// L'outil détient la preuve que l'application préserve le contrat et chaque extension de fournisseur.
    /// Seules ces transformations peuvent être appliquées automatiquement.
    /// </summary>
    Safe,

    /// <summary>
    /// Le remède est probablement utile mais peut changer les clients engendrés, la documentation, la
    /// gouvernance ou le comportement d'une passerelle. Jamais appliqué sans autorisation nominative.
    /// </summary>
    ReviewRequired,

    /// <summary>
    /// Signalé seulement. L'outil n'engendre aucune transformation, sous aucune configuration. Un constat
    /// sans remède automatique porte cette classe.
    /// </summary>
    Unsafe,
}
