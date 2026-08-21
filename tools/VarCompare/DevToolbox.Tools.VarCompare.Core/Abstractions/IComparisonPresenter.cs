using DevToolbox.Tools.VarCompare.Core.Comparison;

namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Affiche une comparaison. Implémenté dans la couche Spectre, le port restant la propriété du cœur.
/// </summary>
/// <remarks>
/// Le contrat qui compte : <see cref="Render"/> n'émet jamais de valeur de variable, quelle que soit la
/// disposition. Il en est incapable, puisqu'une <see cref="ComparisonCell"/> n'en porte aucune.
/// </remarks>
public interface IComparisonPresenter
{
    /// <summary>Affiche la comparaison et sa légende.</summary>
    /// <param name="comparison">La comparaison à afficher.</param>
    /// <param name="options">Choix de disposition et de filtrage.</param>
    void Render(VariableComparison comparison, ComparisonViewOptions options);

    /// <summary>Affiche la légende définissant chaque couleur et chaque symbole employés.</summary>
    void RenderLegend();
}
