using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Demande quelles opérations extraire, la description une fois lue.</summary>
/// <remarks>
/// Un port distinct de l'invite de sélection, et pour une raison de séquence : la liste des opérations
/// vient du document, et le document n'est lu qu'après que le mode a été choisi. Poser la question plus
/// tôt obligerait à faire saisir des chemins à la main, c'est-à-dire exactement le travail que cet écran
/// existe pour éviter.
/// </remarks>
public interface IOperationSelectionPrompt
{
    /// <summary>Demande les opérations à extraire.</summary>
    /// <param name="document">La description lue.</param>
    /// <returns>Les opérations retenues, vides si le développeur renonce.</returns>
    IReadOnlyList<OperationSelector> Ask(SpecDocument document);

    /// <summary>Demande confirmation avant de construire, en disant ce qui sera emporté.</summary>
    /// <param name="selectors">Les opérations retenues.</param>
    /// <param name="components">Le nombre de composants que la fermeture emporterait.</param>
    /// <returns><see langword="true"/> si le développeur confirme.</returns>
    bool Confirm(IReadOnlyList<OperationSelector> selectors, int components);
}
