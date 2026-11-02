using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Les en-têtes que la production déclare en composants, et combien de routes les portent.</summary>
/// <remarks>
/// L'asymétrie que cette fonctionnalité exploite : un contrat de service ne porte pas ces en-têtes — ce
/// sont des conventions de passerelle — mais la production les déclare. L'outil les propose donc parce
/// qu'il les a lus chez le développeur, et non parce qu'il les devine.
/// <para>
/// Seuls les en-têtes sont retenus. Un composant de chemin ou de requête appartient aux routes qui le
/// définissent ; le proposer inviterait à attacher à une route neuve un paramètre qui n'y veut rien dire.
/// </para>
/// <para>
/// Le nombre de routes est compté pour être montré. Un en-tête que toutes les routes portent est une
/// convention de maison ; un en-tête qui apparaît une fois est la particularité d'une route. Les deux ne
/// se proposent pas de la même façon, et l'outil n'a pas à trancher pour le développeur.
/// </para>
/// </remarks>
public static class ReusableHeaders
{
    private const string Components = "components";
    private const string Parameters = "parameters";
    private const string Paths = "paths";
    private const string Reference = "$ref";
    private const string Prefix = "#/components/parameters/";

    /// <summary>Trouve les en-têtes réutilisables d'une production.</summary>
    /// <param name="production">La description en service.</param>
    /// <returns>Les en-têtes référencés au moins une fois, le plus référencé d'abord.</returns>
    /// <remarks>
    /// Un en-tête déclaré que rien ne référence n'est pas offert : il n'établit aucune convention, et le
    /// proposer reviendrait à suggérer au développeur de suivre un usage que personne ne suit.
    /// </remarks>
    public static IReadOnlyList<OfferedHeader> In(SpecDocument production)
    {
        ArgumentNullException.ThrowIfNull(production);

        if (PointerNavigator.Find(production.Root, "/" + Components + "/" + Parameters)
            is not MappingNode declared)
        {
            return [];
        }

        Dictionary<string, int> counted = Counted(production);
        List<OfferedHeader> offered = [];

        foreach (MappingEntry entry in declared.Entries)
        {
            if (entry.Value is not MappingNode parameter || !IsHeader(parameter))
            {
                continue;
            }

            if (!counted.TryGetValue(entry.Key.Text, out int routes) || routes == 0)
            {
                continue;
            }

            offered.Add(new OfferedHeader(entry.Key.Text, Named(parameter, entry.Key.Text), routes));
        }

        return
        [
            .. offered
                .OrderByDescending(header => header.Routes)
                .ThenBy(header => header.Component, StringComparer.Ordinal),
        ];
    }

    /// <summary>Indique que tous les en-têtes offerts sont portés par toutes les routes.</summary>
    /// <param name="offered">Les en-têtes offerts.</param>
    /// <param name="routes">Le nombre d'opérations que la production porte.</param>
    /// <returns><see langword="true"/> quand l'usage est unanime.</returns>
    /// <remarks>
    /// Sert à décider si les cases sont cochées par défaut. Un usage unanime est une convention qu'on
    /// suit sauf raison contraire ; un usage partiel est un choix qu'on fait route par route.
    /// </remarks>
    public static bool Unanimous(IReadOnlyList<OfferedHeader> offered, int routes)
    {
        ArgumentNullException.ThrowIfNull(offered);

        return routes > 0 && offered.Count > 0
            && offered.All(header => header.Routes == routes);
    }

    /// <summary>Combien d'opérations référencent chaque composant de paramètre.</summary>
    private static Dictionary<string, int> Counted(SpecDocument production)
    {
        Dictionary<string, int> counted = new(StringComparer.Ordinal);

        if (PointerNavigator.Find(production.Root, "/" + Paths) is not MappingNode paths)
        {
            return counted;
        }

        foreach (MappingNode item in paths.Entries
            .Select(path => path.Value)
            .OfType<MappingNode>())
        {
            foreach (MappingEntry operation in item.Entries)
            {
                // Les paramètres d'un élément de chemin sont partagés par ses opérations ; ceux d'une
                // opération ne valent que pour elle. Les deux comptent, chacun pour ce qu'il porte.
                Count(operation.Value, counted);
            }

            Count(item, counted);
        }

        return counted;
    }

    /// <summary>Compte les références de paramètre d'un nœud, sans descendre plus loin.</summary>
    private static void Count(SpecNode node, Dictionary<string, int> counted)
    {
        if (node is not MappingNode mapping || mapping.Find(Parameters) is not SequenceNode listed)
        {
            return;
        }

        foreach (SpecNode item in listed.Items)
        {
            if (item is not MappingNode parameter
                || parameter.Find(Reference) is not ScalarNode reference
                || !reference.Text.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string component = reference.Text[Prefix.Length..];

            counted[component] = counted.TryGetValue(component, out int seen) ? seen + 1 : 1;
        }
    }

    /// <summary>Indique qu'un composant de paramètre est un en-tête.</summary>
    private static bool IsHeader(MappingNode parameter) =>
        parameter.Find("in") is ScalarNode where
        && string.Equals(where.Text, "header", StringComparison.OrdinalIgnoreCase);

    /// <summary>Le nom de l'en-tête sur le fil, ou celui du composant à défaut.</summary>
    private static string Named(MappingNode parameter, string fallback) =>
        parameter.Find("name") is ScalarNode named && named.Text.Length > 0 ? named.Text : fallback;
}
