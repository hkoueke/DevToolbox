using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>Relie le port d'appariement au sélecteur en console.</summary>
/// <remarks>
/// Une couche mince, et c'est voulu : le cœur ne doit dépendre que de l'intention « faire confirmer », et
/// la façon de le demander appartient à la présentation. C'est aussi ce qui permet à un essai de l'étape
/// de substituer une réponse — y compris un renoncement — sans toucher à une console.
/// </remarks>
public sealed class CorrespondenceSelectionPrompt : ICorrespondencePrompt
{
    private readonly CorrespondencePicker _picker;

    /// <summary>Crée l'invite.</summary>
    /// <param name="picker">Le sélecteur en console.</param>
    public CorrespondenceSelectionPrompt(CorrespondencePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);

        _picker = picker;
    }

    /// <inheritdoc />
    public Task<OperationCorrespondence> ConfirmAsync(
        OperationCorrespondence proposed,
        IReadOnlyList<string> routes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(routes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_picker.Ask(proposed, routes));
    }
}
