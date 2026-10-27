namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Ce qu'une lecture de contrat s'autorise à résoudre.</summary>
/// <param name="ConfinementRoot">La racine hors de laquelle aucun chemin n'est suivi.</param>
/// <param name="AllowRemote">Suit les emplacements distants.</param>
/// <param name="AllowedHosts">Les hôtes autorisés, quand le distant est permis.</param>
/// <param name="AllowPlainHttp">Autorise <c>http</c> en clair.</param>
/// <param name="MaxRedirects">Le nombre de redirections suivies.</param>
/// <param name="ConfirmedAddresses">Les adresses internes que le développeur a confirmées.</param>
/// <remarks>
/// La résolution locale est active par défaut, et cela diverge délibérément de la politique de référence
/// du mode OpenAPI, qui refuse par défaut ce qui sort du document. La raison n'est pas un appétit de
/// risque différent mais une forme d'entrée différente : un WSDL dont les types vivent dans un schéma
/// voisin est le cas <em>ordinaire</em>, et un défaut fermé refuserait presque tout contrat réel avant
/// qu'on ait pu le configurer.
/// <para>
/// Ce qui paie cette ouverture : la racine de confinement, la traversée refusée en la nommant, et
/// l'obligation d'annoncer le périmètre. Le distant, lui, reste fermé — et le permettre avec une liste
/// d'hôtes vide est refusé plutôt que lu comme « tous ».
/// </para>
/// </remarks>
public sealed record ImportPolicy(
    string ConfinementRoot,
    bool AllowRemote = false,
    IReadOnlyList<string>? AllowedHosts = null,
    bool AllowPlainHttp = false,
    int MaxRedirects = 5,
    IReadOnlyList<string>? ConfirmedAddresses = null)
{
    /// <summary>Les hôtes autorisés, jamais nuls.</summary>
    public IReadOnlyList<string> AllowedHosts { get; init; } = AllowedHosts ?? [];

    /// <summary>
    /// Les adresses internes que le développeur a confirmées, en les nommant.
    /// </summary>
    /// <remarks>
    /// Une adresse, et non un indicateur. Un « autoriser l'interne » global laisserait atteindre tout le
    /// réseau de l'entreprise sur la foi d'une seule case cochée, alors qu'un service interne est le cas
    /// courant et non l'exception : il faut donc pouvoir dire oui, mais à <em>cette</em> adresse-là.
    /// </remarks>
    public IReadOnlyList<string> ConfirmedAddresses { get; init; } = ConfirmedAddresses ?? [];

    /// <summary>Indique que la politique est cohérente.</summary>
    /// <remarks>
    /// Permettre le distant sans nommer aucun hôte n'autorise pas tout : cela ne veut rien dire, et
    /// l'outil refuse plutôt que de choisir une lecture charitable.
    /// </remarks>
    public bool IsCoherent => !AllowRemote || AllowedHosts.Count > 0;

    /// <summary>Une politique confinée à une racine, sans aucun accès distant.</summary>
    /// <param name="root">La racine.</param>
    /// <returns>La politique fermée.</returns>
    public static ImportPolicy ConfinedTo(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return new ImportPolicy(root);
    }
}
