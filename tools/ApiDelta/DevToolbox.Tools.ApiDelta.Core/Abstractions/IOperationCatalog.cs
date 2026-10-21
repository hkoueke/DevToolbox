using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Liste les opérations qu'une description porte, sans en lire le contenu.</summary>
public interface IOperationCatalog
{
    /// <summary>Les opérations sélectionnables, dans un ordre stable.</summary>
    /// <param name="document">La description.</param>
    /// <returns>Une entrée par opération.</returns>
    IReadOnlyList<SelectableOperation> List(SpecDocument document);
}
