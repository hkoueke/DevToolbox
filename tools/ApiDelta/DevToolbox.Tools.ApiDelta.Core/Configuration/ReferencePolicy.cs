namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Jusqu'où une référence a le droit d'aller.</summary>
/// <param name="AllowLocal">Autorise la résolution de fichiers locaux.</param>
/// <param name="AllowRemote">Autorise la résolution par le réseau.</param>
/// <param name="RemoteHosts">Les hôtes autorisés, quand le réseau l'est.</param>
/// <param name="Root">
/// La racine hors de laquelle une référence locale ne doit pas résoudre. Le contrôle porte sur le chemin
/// réel, liens résolus, et non sur le texte de la référence.
/// </param>
/// <remarks>
/// Tout est fermé par défaut, pour deux raisons qui vont dans le même sens : une description qui dépend du
/// réseau ne produit pas deux fois le même résultat, et une référence que l'on suit est une requête que
/// l'on émet vers une adresse choisie par l'auteur du document.
/// </remarks>
public sealed record ReferencePolicy(
    bool AllowLocal = false,
    bool AllowRemote = false,
    IReadOnlyList<string>? RemoteHosts = null,
    string? Root = null)
{
    /// <summary>La politique par défaut : aucune référence externe.</summary>
    public static ReferencePolicy Closed { get; } = new();

    /// <summary>Les hôtes autorisés, jamais nuls.</summary>
    public IReadOnlyList<string> RemoteHosts { get; init; } = RemoteHosts ?? [];

    /// <summary>
    /// Indique si la politique est cohérente : autoriser le réseau sans liste d'hôtes n'est pas une
    /// politique, c'est une porte ouverte.
    /// </summary>
    public bool IsCoherent => !AllowRemote || RemoteHosts.Count > 0;
}
