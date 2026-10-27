namespace DevToolbox.Tools.ApiDelta.Core.Correspondence;

/// <summary>Un appariement entre une opération du contrat et une route de la production.</summary>
/// <param name="Operation">Le nom de l'opération.</param>
/// <param name="Route">La route, méthode et chemin, ou vide si aucune.</param>
/// <param name="Origin">D'où vient l'appariement.</param>
/// <param name="Basis">Sur quoi la proposition s'est fondée, le cas échéant.</param>
/// <param name="Alternatives">Les autres candidats, pour une entrée ambiguë.</param>
/// <remarks>
/// Des chaînes et des énumérations, et rien d'autre : cette forme rejoint le point de reprise, et le
/// magasin vérifie à l'exécution qu'aucun membre ne peut atteindre une valeur. La sélection est la seule
/// chose qui survit à un redémarrage, et une correspondance <em>est</em> une sélection — le développeur
/// l'a confirmée, et la lui redemander à chaque reprise serait lui faire refaire un travail qu'il a fait.
/// <para>
/// La base de la proposition est portée parce qu'un développeur qui voit « d'après les jetons lire,
/// document » peut contredire vite. Devant le seul appariement, il ne peut que l'accepter ou tout
/// reprendre.
/// </para>
/// </remarks>
public sealed record CorrespondenceEntry(
    string Operation,
    string Route,
    CorrespondenceOrigin Origin,
    string? Basis = null,
    IReadOnlyList<string>? Alternatives = null)
{
    /// <summary>Les autres candidats, jamais nuls.</summary>
    public IReadOnlyList<string> Alternatives { get; init; } = Alternatives ?? [];

    /// <summary>Indique que cette entrée désigne une route.</summary>
    public bool HasRoute => !string.IsNullOrWhiteSpace(Route);

    /// <summary>
    /// Indique que l'appariement est déduit plutôt que posé par le développeur.
    /// </summary>
    /// <remarks>
    /// Porté jusqu'à l'écran de sélection, où un élément reposant sur un appariement déduit le dit. Un
    /// développeur qui choisit d'insérer une route doit savoir si la paire sous-jacente vient de lui ou
    /// de l'outil.
    /// </remarks>
    public bool IsInferred => Origin is CorrespondenceOrigin.Proposed or CorrespondenceOrigin.Confirmed;

    /// <summary>La marque qui signale cette entrée à l'écran.</summary>
    public string Mark => Origin switch
    {
        CorrespondenceOrigin.Declared => "✓ déclarée",
        CorrespondenceOrigin.Confirmed => "✓ confirmée",
        CorrespondenceOrigin.Proposed => "~ proposée",
        _ => HasRoute ? "? ambiguë" : "∅ absente",
    };
}
