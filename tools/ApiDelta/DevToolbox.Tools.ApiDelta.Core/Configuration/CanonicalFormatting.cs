namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Comment une description est écrite lorsque l'outil l'écrit.</summary>
/// <param name="Indent">Le nombre d'espaces par niveau.</param>
/// <param name="LineWidth">La largeur au-delà de laquelle une ligne est repliée.</param>
/// <param name="LineEndings">La fin de ligne à employer.</param>
/// <param name="QuoteResponseCodes">Quote les codes de réponse, pour qu'ils restent des chaînes.</param>
/// <param name="FinalNewline">Termine le fichier par une fin de ligne.</param>
/// <param name="KeyOrder">L'ordre des clés dans la sortie.</param>
public sealed record CanonicalFormatting(
    int Indent = 2,
    int LineWidth = 120,
    LineEndingStyle LineEndings = LineEndingStyle.Lf,
    bool QuoteResponseCodes = true,
    bool FinalNewline = true,
    KeyOrderStyle KeyOrder = KeyOrderStyle.Canonical)
{
    /// <summary>La mise en forme par défaut.</summary>
    public static CanonicalFormatting Default { get; } = new();
}
