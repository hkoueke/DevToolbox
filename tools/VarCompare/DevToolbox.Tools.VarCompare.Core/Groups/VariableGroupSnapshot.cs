namespace DevToolbox.Tools.VarCompare.Core.Groups;

/// <summary>
/// Un groupe de variables et son contenu, tels qu'ils étaient au moment de la lecture.
/// </summary>
/// <remarks>
/// <para>
/// La durée de vie est celle du processus, et rien de plus. Un cliché n'est jamais sérialisé et n'est jamais
/// atteignable depuis le point de reprise : c'est ce qui rend structurel, et non affaire de vigilance, le
/// fait qu'aucune valeur de variable ne survive à la session.
/// </para>
/// <para>
/// Le dictionnaire des variables est indexé sans tenir compte de la casse, parce que c'est ainsi qu'Azure
/// DevOps traite les noms de variables de pipeline : <c>API_KEY</c> et <c>Api_Key</c> ne font qu'une seule
/// variable pour la plateforme, et les traiter comme deux inventerait une différence qui n'existe pas.
/// </para>
/// </remarks>
public sealed class VariableGroupSnapshot
{
    private readonly Dictionary<string, VariableEntry> _variables;

    /// <summary>Crée un cliché.</summary>
    /// <param name="summary">Le groupe auquel ce cliché appartient.</param>
    /// <param name="variables">Les variables lues dans le groupe.</param>
    /// <param name="retrievedAt">Quand le groupe a été lu, selon l'horloge injectée.</param>
    /// <param name="modifiedOn">Quand le serveur a enregistré un changement pour la dernière fois. Informatif.</param>
    /// <param name="isDegraded">
    /// Si la lecture n'a que partiellement abouti — un coffre de clés injoignable, par exemple. Chaque
    /// cellule d'un groupe dégradé rapporte <see cref="Comparison.CellState.Undetermined"/> au lieu de
    /// paraître vide.
    /// </param>
    public VariableGroupSnapshot(
        VariableGroupSummary summary,
        IEnumerable<VariableEntry> variables,
        DateTimeOffset retrievedAt,
        DateTimeOffset? modifiedOn = null,
        bool isDegraded = false)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(variables);

        _variables = new Dictionary<string, VariableEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (VariableEntry entry in variables)
        {
            // La dernière écriture l'emporte. La plateforme ne peut pas détenir deux noms qui ne diffèrent
            // que par la casse : un doublon ici signifie que le serveur en a envoyé un, et garder l'un ou
            // l'autre revient au même.
            _variables[entry.Name] = entry;
        }

        Summary = summary;
        RetrievedAt = retrievedAt;
        ModifiedOn = modifiedOn;
        IsDegraded = isDegraded;
    }

    /// <summary>Le groupe auquel ce cliché appartient.</summary>
    public VariableGroupSummary Summary { get; }

    /// <summary>Quand le groupe a été lu.</summary>
    public DateTimeOffset RetrievedAt { get; }

    /// <summary>
    /// Quand le serveur a enregistré un changement pour la dernière fois. Affiché, mais ne protège rien :
    /// sans chemin d'écriture, il n'y a aucune concurrence à détecter.
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; }

    /// <summary>Indique si la lecture n'a que partiellement abouti.</summary>
    public bool IsDegraded { get; }

    /// <summary>Le nombre de variables du groupe.</summary>
    public int Count => _variables.Count;

    /// <summary>Tous les noms de variables du groupe, tels qu'ils sont stockés.</summary>
    public IEnumerable<string> Names => _variables.Keys;

    /// <summary>Toutes les variables du groupe.</summary>
    public IEnumerable<VariableEntry> Entries => _variables.Values;

    /// <summary>Recherche une variable par son nom, sans tenir compte de la casse.</summary>
    /// <param name="name">Le nom recherché.</param>
    /// <returns>L'entrée, ou <see langword="null"/> si ce groupe ne la possède pas.</returns>
    public VariableEntry? Find(string name) =>
        _variables.TryGetValue(name, out VariableEntry? entry) ? entry : null;
}
