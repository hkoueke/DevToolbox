namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Toutes les routes composées d'une exécution.</summary>
/// <param name="Routes">Dans l'ordre où le développeur les a composées.</param>
/// <remarks>
/// L'ordre de composition est gardé parce qu'il est celui des points d'insertion : une route placée
/// « après la précédente » désigne la précédente de cette liste, et réordonner la liste changerait où les
/// routes atterrissent.
/// </remarks>
public sealed record AuthoringRecord(IReadOnlyList<AuthoredRoute> Routes)
{
    /// <summary>Les routes, jamais nulles.</summary>
    public IReadOnlyList<AuthoredRoute> Routes { get; init; } = Routes ?? [];

    /// <summary>Le registre d'une exécution où rien n'a été composé.</summary>
    /// <remarks>
    /// Existe pour que l'étape ait une valeur à porter quand le développeur ne retient rien : une étape
    /// qui ne peut pas rendre « rien » finit par rendre null, et le reste de la chaîne apprend à tester
    /// null partout.
    /// </remarks>
    public static AuthoringRecord Empty { get; } = new([]);

    /// <summary>Indique qu'aucune route n'a été composée.</summary>
    public bool IsEmpty => Routes.Count == 0;

    /// <summary>Les champs écartés, avec l'opération dont ils viennent.</summary>
    /// <returns>Un couple par champ écarté.</returns>
    public IEnumerable<(string Operation, FieldPlacement Field)> Dropped() =>
        Routes.SelectMany(route => route.Dropped.Select(field => (route.Operation, field)));

    /// <summary>Les routes dont la description reste à écrire.</summary>
    /// <returns>Une entrée par route composée.</returns>
    /// <remarks>
    /// Une par route, sans exception : aucune route composée ne porte de description que l'outil aurait
    /// rédigée. Le compte sert au rapport, pour que le nombre de descriptions à écrire soit connu sans
    /// ouvrir le fichier.
    /// </remarks>
    public IEnumerable<AuthoredRoute> Placeholders() => Routes;

    /// <summary>Les collisions, entre routes composées et avec celles de la production.</summary>
    /// <param name="production">Les routes que la production porte déjà, telles qu'elle les désigne.</param>
    /// <returns>Une phrase par collision, nommant ses deux côtés.</returns>
    /// <remarks>
    /// Rendues et non levées : l'appelant nomme les deux côtés, et une collision n'est pas une faute de
    /// programmation mais une réponse du développeur qu'il faut lui rendre.
    /// </remarks>
    public IReadOnlyList<string> Collisions(IReadOnlyList<string> production)
    {
        ArgumentNullException.ThrowIfNull(production);

        List<string> found = [];
        HashSet<string> held = new(production, StringComparer.Ordinal);
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (AuthoredRoute route in Routes)
        {
            string display = route.Display;

            if (held.Contains(display))
            {
                found.Add(
                    "« " + route.Operation + " » est composée sur " + display
                    + ", que la production porte déjà.");
            }

            if (seen.TryGetValue(display, out string? earlier))
            {
                found.Add(
                    "« " + route.Operation + " » et « " + earlier + " » sont composées toutes deux sur "
                    + display + ".");

                continue;
            }

            seen[display] = route.Operation;
        }

        return found;
    }
}
