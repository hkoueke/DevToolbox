namespace DevToolbox.Domain.Runs;

/// <summary>
/// Porte l'identité d'une exécution ainsi que ce qu'une étape doit transmettre à la suivante. Cet état ne
/// vit que le temps du processus : rien de cet objet n'est écrit sur disque, et c'est précisément ce qui
/// tient les valeurs de variables à l'écart du fichier de reprise.
/// </summary>
public sealed class ToolRunContext
{
    private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);

    /// <summary>Crée un contexte pour une exécution.</summary>
    /// <param name="run">L'exécution à laquelle ce contexte appartient.</param>
    public ToolRunContext(ToolRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Run = run;
    }

    /// <summary>L'exécution à laquelle ce contexte appartient.</summary>
    public ToolRun Run { get; }

    /// <summary>Dépose une valeur à l'intention d'une étape ultérieure.</summary>
    /// <typeparam name="T">Le type de la valeur.</typeparam>
    /// <param name="key">La clé sous laquelle la déposer.</param>
    /// <param name="value">La valeur.</param>
    public void Set<T>(string key, T value)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _items[key] = value;
    }

    /// <summary>Lit une valeur déposée par une étape antérieure.</summary>
    /// <typeparam name="T">Le type de la valeur.</typeparam>
    /// <param name="key">La clé sous laquelle elle a été déposée.</param>
    /// <returns>La valeur, ou <see langword="default"/> si elle est absente ou d'un autre type.</returns>
    public T? Get<T>(string key) =>
        _items.TryGetValue(key, out object? value) && value is T typed ? typed : default;

    /// <summary>Indique si une valeur existe sous la clé donnée.</summary>
    /// <param name="key">La clé à tester.</param>
    /// <returns><see langword="true"/> si elle est présente.</returns>
    public bool Has(string key) => _items.ContainsKey(key);

    /// <summary>
    /// Jette tout ce que les étapes ont déposé. Utilisé lors d'une relance complète, afin que l'exécution
    /// relise réellement les données au lieu de réutiliser des clichés pris avant l'échec.
    /// </summary>
    public void Clear() => _items.Clear();
}
