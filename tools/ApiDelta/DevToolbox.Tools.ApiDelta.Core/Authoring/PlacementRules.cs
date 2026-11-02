namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Ce qu'un placement ne peut pas être, et ce qu'une route composée doit tenir.</summary>
/// <remarks>
/// Séparé de <see cref="PlacementDefaults" /> parce qu'un refus et une proposition ne sont pas la même
/// chose. Une proposition s'accepte ou se change ; un refus s'explique et ne se contourne pas. Les mêler
/// dans un seul type aurait fait porter au même code deux responsabilités dont une seule peut dire non.
/// <para>
/// Chaque refus nomme son sujet. Un refus qui dirait « placement invalide » obligerait le développeur à
/// chercher lequel, parmi vingt champs, et c'est le genre de message qu'on finit par ignorer.
/// </para>
/// </remarks>
public static class PlacementRules
{
    /// <summary>Vérifie un placement seul.</summary>
    /// <param name="placement">Le placement à vérifier.</param>
    /// <returns>Le refus, ou <see langword="null" /> quand il n'y a rien à dire.</returns>
    /// <remarks>
    /// Le composite mal placé est déjà refusé par <see cref="FieldPlacement" /> lui-même, qui lève. Cette
    /// fonction existe pour le chemin d'invite, où l'on veut une phrase à montrer avant de construire
    /// l'objet — demander pardon plutôt que permission ferait remonter une exception à travers une
    /// conversation.
    /// </remarks>
    public static string? Check(FieldPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (!placement.IsComposite)
        {
            return null;
        }

        return placement.Location switch
        {
            ParameterLocation.Path or ParameterLocation.Query or ParameterLocation.Header =>
                "« " + placement.Field + " » est de type " + placement.Declared + ", qui n'est pas un "
                + "scalaire : il ne peut pas voyager dans le "
                + FieldPlacement.Where(placement.Location)
                + ". Placez-le dans le corps, ou changez le verbe.",
            _ => null,
        };
    }

    /// <summary>Vérifie une route composée dans son ensemble.</summary>
    /// <param name="route">La route composée.</param>
    /// <returns>Les refus, un par règle enfreinte, jamais nuls.</returns>
    /// <remarks>
    /// Deux règles qu'un placement seul ne peut pas tenir, parce qu'elles portent sur l'ensemble : chaque
    /// marqueur du chemin doit être rempli par un champ placé dans le chemin sous ce nom, et deux
    /// paramètres d'une même destination ne peuvent pas porter le même nom effectif.
    /// </remarks>
    public static IReadOnlyList<string> Check(AuthoredRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        List<string> refusals = [];

        refusals.AddRange(Unfilled(route));
        refusals.AddRange(Doubled(route));

        return refusals;
    }

    /// <summary>Vérifie qu'un placement est possible, avant de construire l'objet.</summary>
    /// <param name="composite">Si le type du champ n'est pas un scalaire.</param>
    /// <param name="location">La destination envisagée.</param>
    /// <returns><see langword="true" /> quand le placement se tient.</returns>
    public static bool Allows(bool composite, ParameterLocation location) =>
        !composite || location is ParameterLocation.Body or ParameterLocation.Nowhere;

    /// <summary>Les marqueurs du chemin que nul champ ne remplit, et l'inverse.</summary>
    private static IEnumerable<string> Unfilled(AuthoredRoute route)
    {
        HashSet<string> placed = new(
            route.Placements
                .Where(placement => placement.Location is ParameterLocation.Path)
                .Select(placement => placement.Effective),
            StringComparer.Ordinal);

        IReadOnlyList<string> expected = RouteSyntax.Placeholders(route.Path);

        foreach (string name in expected.Where(name => !placed.Contains(name)))
        {
            yield return "« {" + name + "} » figure dans le chemin « " + route.Path
                + " », et aucun champ n'y est placé sous ce nom.";
        }

        HashSet<string> wanted = new(expected, StringComparer.Ordinal);

        foreach (string name in placed.Where(name => !wanted.Contains(name)))
        {
            yield return "« " + name + " » est placé dans le chemin, que « " + route.Path
                + " » ne prévoit pas : ajoutez « {" + name + "} » au chemin, ou placez-le ailleurs.";
        }
    }

    /// <summary>Les noms effectifs employés deux fois dans une même destination.</summary>
    private static IEnumerable<string> Doubled(AuthoredRoute route)
    {
        IEnumerable<IGrouping<(ParameterLocation, string), FieldPlacement>> grouped = route.Placements
            .Where(placement => !placement.IsDropped)
            .GroupBy(placement => (placement.Location, placement.Effective));

        foreach (IGrouping<(ParameterLocation Location, string Name), FieldPlacement> group in
            grouped.Where(group => group.Count() > 1))
        {
            yield return "« " + group.Key.Name + " » est employé " + group.Count().ToString(
                System.Globalization.CultureInfo.InvariantCulture)
                + " fois dans le " + FieldPlacement.Where(group.Key.Location)
                + " : donnez un nom distinct, ou placez l'un des deux ailleurs.";
        }
    }
}
