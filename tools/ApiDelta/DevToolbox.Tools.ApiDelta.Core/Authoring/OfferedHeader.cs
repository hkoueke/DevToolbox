namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Un paramètre d'en-tête que la production déclare en composant réutilisable.</summary>
/// <param name="Component">Le nom du composant, sous <c>components/parameters</c>.</param>
/// <param name="Name">Le nom de l'en-tête sur le fil, pour l'afficher.</param>
/// <param name="Routes">Combien d'opérations de la production le référencent.</param>
/// <remarks>
/// Un contrat de service ne porte pas ces en-têtes : ce sont des conventions de passerelle, pas du
/// contrat. La production, elle, les déclare — et c'est l'asymétrie que cette fonctionnalité exploite.
/// L'outil les propose parce qu'il les a lus chez le développeur, non parce qu'il les devine.
/// <para>
/// Le nombre de routes est porté pour être montré : un en-tête que toutes les routes portent est une
/// convention de maison, un en-tête qui apparaît une fois est la particularité d'une route. Les deux ne
/// se proposent pas de la même façon.
/// </para>
/// </remarks>
public sealed record OfferedHeader(string Component, string Name, int Routes)
{
    /// <summary>Ce que l'en-tête dit, en une ligne.</summary>
    public string Display => Name + " (" + Routes.ToString(
        System.Globalization.CultureInfo.InvariantCulture) + " route(s))";

    /// <summary>L'emplacement du composant, tel qu'une référence le désigne.</summary>
    public string Reference => "#/components/parameters/" + Component;
}
