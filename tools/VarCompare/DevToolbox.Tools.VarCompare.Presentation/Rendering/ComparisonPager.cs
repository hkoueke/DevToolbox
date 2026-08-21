using DevToolbox.Tools.VarCompare.Core.Comparison;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Découpe une longue comparaison en pages qui tiennent dans le terminal.
/// </summary>
/// <remarks>
/// Déverser cinq cents lignes dans l'historique du terminal, c'est le problème que cet outil existe pour
/// remplacer, pas une stratégie d'affichage. Cela ferait de surcroît défiler la légende hors de l'écran,
/// alors qu'elle doit rester avec la comparaison. La pagination garde chaque ligne atteignable et la
/// légende visible.
/// </remarks>
public static class ComparisonPager
{
    /// <summary>Nombre de lignes par page lorsque la hauteur du terminal ne peut pas être mesurée.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Le nombre minimal de lignes d'une page, quelle que soit la hauteur du terminal.</summary>
    public const int MinimumPageSize = 5;

    /// <summary>
    /// Les lignes d'habillage qui ne sont pas des lignes de comparaison : bordures, en-tête, décomptes et
    /// légende.
    /// </summary>
    public const int ChromeRows = 18;

    /// <summary>Prend une page de lignes.</summary>
    /// <param name="rows">Les lignes filtrées, dans l'ordre d'affichage.</param>
    /// <param name="pageNumber">La page à prendre, à partir de un. Ramenée dans les bornes.</param>
    /// <param name="pageSize">Combien de lignes tiennent sur une page.</param>
    /// <returns>La page.</returns>
    public static ComparisonPage Page(
        IReadOnlyList<ComparisonRow> rows,
        int pageNumber,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        if (rows.Count == 0)
        {
            return new ComparisonPage([], 0, 0, 0, 1, 1);
        }

        int pageCount = (rows.Count + pageSize - 1) / pageSize;
        int page = Math.Clamp(pageNumber, 1, pageCount);
        int skip = (page - 1) * pageSize;
        int take = Math.Min(pageSize, rows.Count - skip);

        return new ComparisonPage(
            [.. rows.Skip(skip).Take(take)],
            skip + 1,
            skip + take,
            rows.Count,
            page,
            pageCount);
    }

    /// <summary>Calcule combien de lignes tiennent sur une page dans ce terminal.</summary>
    /// <param name="terminalHeight">La hauteur utile du terminal.</param>
    /// <param name="layout">La disposition affichée.</param>
    /// <returns>Une taille de page d'au moins <see cref="MinimumPageSize"/>.</returns>
    public static int PageSizeFor(int terminalHeight, ComparisonLayout layout)
    {
        if (terminalHeight <= 0)
        {
            return DefaultPageSize;
        }

        // Une entrée empilée occupe son propre panneau : elle coûte donc environ une ligne par groupe, plus
        // sa bordure.
        int rowsAvailable = layout == ComparisonLayout.Stacked
            ? (terminalHeight - ChromeRows) / 4
            : terminalHeight - ChromeRows;

        return Math.Max(MinimumPageSize, rowsAvailable);
    }
}
