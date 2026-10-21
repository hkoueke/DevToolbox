namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Ce qu'un scalaire se révèle être après résolution.</summary>
public enum ScalarValueKind
{
    /// <summary>Une chaîne de texte.</summary>
    Text,

    /// <summary>Un entier.</summary>
    Integral,

    /// <summary>Un nombre à virgule.</summary>
    Number,

    /// <summary>Un booléen.</summary>
    Boolean,

    /// <summary>L'absence de valeur.</summary>
    Null,

    /// <summary>
    /// Une écriture dont la résolution n'est pas univoque, signalée plutôt que tranchée.
    /// </summary>
    Ambiguous,
}
