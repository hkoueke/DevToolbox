namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>Ce qu'un champ engage.</summary>
public enum FieldRole
{
    /// <summary>Le contrat : ce qu'un appelant peut constater.</summary>
    Contract,

    /// <summary>La documentation : ce qu'un lecteur lit, sans effet sur le contrat.</summary>
    Documentation,

    /// <summary>
    /// Une extension de fournisseur. Traitée comme susceptible d'agir sur le comportement, faute de
    /// pouvoir établir le contraire : une passerelle peut lire ce que l'outil ne comprend pas.
    /// </summary>
    Vendor,
}
