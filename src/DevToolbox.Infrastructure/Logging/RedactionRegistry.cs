using System.Collections.Concurrent;

namespace DevToolbox.Infrastructure.Logging;

/// <summary>
/// L'ensemble des types qui ne doivent jamais atteindre un puits de journalisation. Chaque outil y
/// enregistre au démarrage ses propres types porteurs de valeurs, la couche d'infrastructure partagée ne
/// pouvant pas référencer un outil.
/// </summary>
/// <remarks>
/// Le filtrage porte volontairement sur le type et non sur le contenu. Inspecter le texte d'un message à la
/// recherche de ce qui « ressemble » à un secret est une heuristique qui se trompe silencieusement dans les
/// deux sens ; refuser un argument à cause de ce qu'il <em>est</em> ne peut pas produire de faux négatif
/// pour les types enregistrés.
/// </remarks>
public sealed class RedactionRegistry
{
    private readonly ConcurrentDictionary<Type, byte> _forbidden = new();

    /// <summary>Enregistre un type qui ne doit jamais être passé à un journal.</summary>
    /// <param name="type">Le type interdit.</param>
    public void Forbid(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        _forbidden.TryAdd(type, 0);
    }

    /// <summary>Enregistre un type qui ne doit jamais être passé à un journal.</summary>
    /// <typeparam name="T">Le type interdit.</typeparam>
    public void Forbid<T>() => Forbid(typeof(T));

    /// <summary>Indique si une valeur de ce type doit être masquée avant d'atteindre un puits.</summary>
    /// <param name="type">Le type à tester.</param>
    /// <returns><see langword="true"/> si le type, ou une séquence de ce type, est interdit.</returns>
    public bool IsForbidden(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_forbidden.ContainsKey(type))
        {
            return true;
        }

        // Une collection d'un type interdit est tout aussi dangereuse que le type lui-même.
        foreach (Type candidate in _forbidden.Keys)
        {
            if (candidate.IsAssignableFrom(type))
            {
                return true;
            }

            if (typeof(IEnumerable<>).MakeGenericType(candidate).IsAssignableFrom(type))
            {
                return true;
            }
        }

        return false;
    }
}
