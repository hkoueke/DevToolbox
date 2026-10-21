namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>La manière dont un scalaire était écrit.</summary>
/// <remarks>
/// Conservée parce que la mise en forme est un fait du document : c'est ce qui permet de signaler que deux
/// codes de réponse sont quotés différemment sans prétendre pour autant qu'il s'agit de deux codes.
/// </remarks>
public enum ScalarStyle
{
    /// <summary>Sans guillemets.</summary>
    Plain,

    /// <summary>Entre apostrophes.</summary>
    SingleQuoted,

    /// <summary>Entre guillemets.</summary>
    DoubleQuoted,

    /// <summary>Bloc littéral, qui conserve les fins de ligne.</summary>
    Literal,

    /// <summary>Bloc replié, qui les replie.</summary>
    Folded,
}
