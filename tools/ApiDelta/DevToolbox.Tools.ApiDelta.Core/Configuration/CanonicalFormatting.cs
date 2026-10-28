namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Comment une description est écrite lorsque l'outil l'écrit.</summary>
/// <param name="Indent">Le nombre d'espaces par niveau.</param>
/// <param name="LineWidth">La largeur au-delà de laquelle une ligne est repliée.</param>
/// <param name="LineEndings">La fin de ligne à employer.</param>
/// <param name="QuoteResponseCodes">Quote les codes de réponse, pour qu'ils restent des chaînes.</param>
/// <param name="FinalNewline">Termine le fichier par une fin de ligne.</param>
/// <param name="KeyOrder">L'ordre des clés dans la sortie.</param>
/// <remarks>
/// <see cref="KeyOrderStyle.Preserve" /> est le défaut, et c'est un choix d'usage plutôt que de
/// technique : les fichiers que cet outil écrit sont destinés à remplacer un document tenu à la main,
/// relu par une personne et validé dans un dépôt. Un tri alphabétique rendait ce document
/// méconnaissable — un écart de plusieurs milliers de lignes pour une route ajoutée — et faisait porter
/// à la relecture le coût d'une reproductibilité que l'empreinte du contenu établit déjà. Le tri reste
/// disponible pour qui le veut, par « formatting.keyOrder: canonical » dans le jeu de règles.
/// </remarks>
public sealed record CanonicalFormatting(
    int Indent = 2,
    int LineWidth = 120,
    LineEndingStyle LineEndings = LineEndingStyle.Lf,
    bool QuoteResponseCodes = true,
    bool FinalNewline = true,
    KeyOrderStyle KeyOrder = KeyOrderStyle.Preserve)
{
    /// <summary>La mise en forme par défaut.</summary>
    public static CanonicalFormatting Default { get; } = new();
}
