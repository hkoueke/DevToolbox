namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Le rôle qu'une description tient dans une exécution.</summary>
public enum DocumentRole
{
    /// <summary>La description en production, référence de la comparaison et sujet de l'audit.</summary>
    Production,

    /// <summary>La description candidate, celle dont on cherche l'effet sur la production.</summary>
    Candidate,
}
