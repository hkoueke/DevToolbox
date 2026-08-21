using System.Globalization;

namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>
/// Décide si la grille en colonnes tient à l'écran, et avertit avant de basculer vers l'affichage empilé.
/// </summary>
/// <remarks>
/// Le seuil est mesuré, pas deviné : la largeur réelle du terminal est comparée à celle dont les colonnes
/// ont effectivement besoin. Il n'y a volontairement aucun nombre maximal de groupes écrit en dur, car
/// savoir si cinq groupes tiennent dépend entièrement de la largeur du terminal et de la longueur des noms.
/// </remarks>
public static class LayoutSelector
{
    /// <summary>La largeur nécessaire à une cellule d'état : symbole, mot et les deux marques facultatives.</summary>
    public const int CellWidth = 20;

    /// <summary>La largeur minimale autorisée pour la colonne des noms de variables.</summary>
    public const int MinimumNameColumnWidth = 16;

    /// <summary>Le surcoût de bordure et de marge par colonne dans un tableau Spectre arrondi.</summary>
    public const int ColumnChrome = 3;

    /// <summary>Détermine quelle disposition afficher.</summary>
    /// <param name="requested">Ce que le développeur a demandé. <c>Auto</c> mesure, les autres sont honorées.</param>
    /// <param name="comparison">La comparaison à afficher.</param>
    /// <param name="terminalWidth">La largeur utile du terminal.</param>
    /// <returns>La disposition retenue, et s'il faut avertir au préalable.</returns>
    public static LayoutDecision Decide(
        ComparisonLayout requested,
        VariableComparison comparison,
        int terminalWidth)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        // Un choix manuel est toujours honoré, quel que soit le nombre de groupes.
        if (requested != ComparisonLayout.Auto)
        {
            return new LayoutDecision(requested, WarnBeforeRendering: false, Reason: null);
        }

        int required = RequiredWidth(comparison);

        if (required <= terminalWidth)
        {
            return new LayoutDecision(ComparisonLayout.SideBySide, WarnBeforeRendering: false, Reason: null);
        }

        string reason = string.Create(
            CultureInfo.InvariantCulture,
            $"{comparison.Groups.Count} groupes demandent environ {required} colonnes de largeur et le "
                + $"terminal en offre {terminalWidth}. Passage à la disposition empilée pour que rien ne "
                + $"soit tronqué.");

        return new LayoutDecision(ComparisonLayout.Stacked, WarnBeforeRendering: true, reason);
    }

    /// <summary>La largeur dont la grille en colonnes a besoin pour s'afficher sans troncature.</summary>
    /// <param name="comparison">La comparaison à mesurer.</param>
    /// <returns>Une largeur en colonnes de terminal.</returns>
    public static int RequiredWidth(VariableComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        int longestName = comparison.Rows.Count == 0
            ? MinimumNameColumnWidth
            : comparison.Rows.Max(row => row.DisplayName.Length);

        int nameColumn = Math.Max(MinimumNameColumnWidth, longestName) + ColumnChrome;

        return nameColumn + (comparison.Groups.Count * (CellWidth + ColumnChrome));
    }

    /// <summary>
    /// Indique si une disposition peut montrer tous les groupes et toutes les lignes de cette comparaison.
    /// </summary>
    /// <param name="layout">La disposition retenue.</param>
    /// <param name="comparison">La comparaison.</param>
    /// <param name="terminalWidth">La largeur utile du terminal.</param>
    /// <returns>
    /// <see langword="true"/> si tout tient. Dans le cas contraire, l'appelant doit le dire plutôt que
    /// d'afficher un résultat partiel.
    /// </returns>
    public static bool CanShowEverything(
        ComparisonLayout layout,
        VariableComparison comparison,
        int terminalWidth)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        // L'affichage empilé présente une variable à la fois : son besoin en largeur ne croît donc pas avec
        // le nombre de groupes. Il n'échoue que sur un terminal trop étroit pour un seul état libellé.
        return layout == ComparisonLayout.Stacked
            ? terminalWidth >= MinimumNameColumnWidth
            : RequiredWidth(comparison) <= terminalWidth;
    }
}
