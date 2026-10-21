namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>L'incrément qu'un changement commande.</summary>
public enum VersionImpact
{
    /// <summary>Aucun : rien n'a changé.</summary>
    None,

    /// <summary>Correctif : documentation et métadonnées sans effet sur le comportement.</summary>
    Patch,

    /// <summary>Mineur : un ajout compatible.</summary>
    Minor,

    /// <summary>Majeur : un changement cassant ou restrictif.</summary>
    Major,
}
