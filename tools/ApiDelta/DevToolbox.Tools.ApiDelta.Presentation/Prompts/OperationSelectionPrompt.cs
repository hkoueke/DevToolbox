using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>
/// Relie l'étape d'extraction au catalogue et au sélecteur.
/// </summary>
/// <remarks>
/// Un adaptateur, et rien de plus : le catalogue sait lire la description, le sélecteur sait demander, et
/// ni l'un ni l'autre n'a besoin de connaître l'autre. L'étape, elle, ne connaît que ce port.
/// </remarks>
public sealed class OperationSelectionPrompt : IOperationSelectionPrompt
{
    private readonly IOperationCatalog _catalogue;
    private readonly OperationPicker _picker;

    /// <summary>Crée l'invite.</summary>
    /// <param name="catalogue">Liste les opérations de la description.</param>
    /// <param name="picker">Demande lesquelles retenir.</param>
    public OperationSelectionPrompt(IOperationCatalog catalogue, OperationPicker picker)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(picker);

        _catalogue = catalogue;
        _picker = picker;
    }

    /// <inheritdoc />
    public IReadOnlyList<OperationSelector> Ask(SpecDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return _picker.Ask(_catalogue.List(document), Path.GetFileName(document.Path));
    }

    /// <inheritdoc />
    public bool Confirm(IReadOnlyList<OperationSelector> selectors, int components) =>
        _picker.Confirm(selectors, components);
}
