namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Comment un type dérive d'un autre.</summary>
public enum XsdDerivation
{
    /// <summary>Il ne dérive de rien.</summary>
    None,

    /// <summary>Par extension : il ajoute. Porté approximativement, vers <c>allOf</c>.</summary>
    Extension,

    /// <summary>
    /// Par restriction : il resserre. Aucun équivalent, car JSON Schema ne sait pas resserrer un schéma
    /// qu'il référence.
    /// </summary>
    Restriction,
}
