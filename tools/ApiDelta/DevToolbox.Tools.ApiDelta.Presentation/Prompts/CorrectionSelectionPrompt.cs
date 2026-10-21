using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Correction;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>
/// Relie l'étape de correction au sélecteur.
/// </summary>
/// <remarks>
/// Un adaptateur, et rien de plus : l'étape ne connaît que ce port, et le sélecteur ne connaît que
/// Spectre. C'est ce qui permet d'éprouver l'étape sans terminal.
/// </remarks>
public sealed class CorrectionSelectionPrompt : ICorrectionSelectionPrompt
{
    private readonly CorrectionPicker _picker;

    /// <summary>Crée l'invite.</summary>
    /// <param name="picker">Demande ce qu'il faut porter, et fait accepter l'énoncé.</param>
    public CorrectionSelectionPrompt(CorrectionPicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);

        _picker = picker;
    }

    /// <inheritdoc />
    public CorrectionSelection Ask(IReadOnlyList<CorrectionRoute> routes) => _picker.Ask(routes);

    /// <inheritdoc />
    public bool Confirm(CorrectionPlan plan) => _picker.Confirm(plan);
}
