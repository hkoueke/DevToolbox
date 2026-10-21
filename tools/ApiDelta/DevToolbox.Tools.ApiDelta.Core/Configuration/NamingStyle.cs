namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>La forme qu'un nom est censé prendre.</summary>
public enum NamingStyle
{
    /// <summary>Aucune convention : la règle ne dit rien.</summary>
    Any,

    /// <summary>Capitale initiale minuscule, mots accolés.</summary>
    Camel,

    /// <summary>Capitale initiale majuscule, mots accolés.</summary>
    Pascal,

    /// <summary>Minuscules, mots séparés par des traits d'union.</summary>
    Kebab,

    /// <summary>Minuscules, mots séparés par des soulignés.</summary>
    Snake,
}
