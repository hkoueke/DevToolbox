namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>Ce qu'un changement fait aux appelants existants.</summary>
public enum ChangeImpact
{
    /// <summary>Un appelant existant cesse de fonctionner, ou se voit refuser ce qui passait.</summary>
    Breaking,

    /// <summary>Un appelant existant continue de fonctionner à l'identique.</summary>
    NonBreaking,

    /// <summary>Rien d'observable ne change : seule la lecture humaine en est affectée.</summary>
    DocumentationOnly,

    /// <summary>
    /// L'outil ne peut pas l'établir. Jamais présenté comme inoffensif : un doute annoncé se vérifie, un
    /// doute tu se découvre en production.
    /// </summary>
    Unknown,
}
