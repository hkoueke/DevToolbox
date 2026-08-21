using DevToolbox.Tools.VarCompare.Core.Comparison;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>Une page d'une longue comparaison.</summary>
/// <param name="Rows">Les lignes de cette page.</param>
/// <param name="FirstRowNumber">Le numéro de la première ligne affichée, à partir de un.</param>
/// <param name="LastRowNumber">Le numéro de la dernière ligne affichée, à partir de un.</param>
/// <param name="TotalRows">Combien de lignes compte la comparaison filtrée au total.</param>
/// <param name="PageNumber">Le numéro de page, à partir de un.</param>
/// <param name="PageCount">Le nombre de pages.</param>
public sealed record ComparisonPage(
    IReadOnlyList<ComparisonRow> Rows,
    int FirstRowNumber,
    int LastRowNumber,
    int TotalRows,
    int PageNumber,
    int PageCount)
{
    /// <summary>Indique si la comparaison tient sur plus d'une page.</summary>
    public bool IsPaged => PageCount > 1;

    /// <summary>Indique s'il existe une page après celle-ci.</summary>
    public bool HasNext => PageNumber < PageCount;

    /// <summary>Indique s'il existe une page avant celle-ci.</summary>
    public bool HasPrevious => PageNumber > 1;
}
