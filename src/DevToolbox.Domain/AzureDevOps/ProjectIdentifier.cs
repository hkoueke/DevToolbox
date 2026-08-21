namespace DevToolbox.Domain.AzureDevOps;

/// <summary>
/// Un projet au sein d'une collection, identifié par son id une fois résolu et par son nom jusque-là.
/// </summary>
/// <remarks>
/// Placé dans le noyau partagé plutôt que dans un outil : le second outil aura besoin de la même notion de
/// projet, et la dupliquer par outil viderait de son sens le fait de mutualiser l'accès à Azure DevOps.
/// </remarks>
public sealed class ProjectIdentifier : IEquatable<ProjectIdentifier>
{
    /// <summary>Longueur maximale d'un nom de projet au-delà de laquelle il est jugé malformé.</summary>
    public const int MaxNameLength = 64;

    /// <summary>Crée un identifiant de projet.</summary>
    /// <param name="name">Le nom du projet tel que le serveur l'orthographie.</param>
    /// <param name="id">L'id du projet, lorsqu'il est déjà résolu.</param>
    /// <exception cref="ArgumentException">Le nom est vide ou ne peut pas figurer dans une URL.</exception>
    public ProjectIdentifier(string name, Guid? id = null)
    {
        if (!IsSafeSegment(name))
        {
            throw new ArgumentException(
                "A project name must be 1 to 64 characters and must not contain a path separator, '..', "
                + "or a control character.",
                nameof(name));
        }

        Name = name;
        Id = id;
    }

    /// <summary>L'id du projet, présent une fois résolu auprès du point d'accès des projets.</summary>
    public Guid? Id { get; }

    /// <summary>Le nom du projet tel qu'il est stocké sur le serveur.</summary>
    public string Name { get; }

    /// <summary>
    /// Le segment à placer dans une URL : l'id lorsqu'il est connu, sinon le nom échappé.
    /// </summary>
    /// <returns>Un segment échappé, sûr pour une URL.</returns>
    public string ToRouteSegment() =>
        Id is { } id ? id.ToString("D") : Uri.EscapeDataString(Name);

    /// <summary>Indique si un nom candidat peut être placé sans risque dans une URL.</summary>
    /// <param name="value">Le candidat.</param>
    /// <returns><see langword="true"/> si la valeur est sûre.</returns>
    public static bool IsSafeSegment(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxNameLength
        && !value.Contains('/', StringComparison.Ordinal)
        && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains("..", StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    /// <inheritdoc />
    public bool Equals(ProjectIdentifier? other)
    {
        if (other is null)
        {
            return false;
        }

        // Azure DevOps ne distingue pas la casse des noms de projet, et l'id prime dès que les deux côtés
        // en possèdent un.
        return Id is not null && other.Id is not null
            ? Id == other.Id
            : string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ProjectIdentifier);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    /// <inheritdoc />
    public override string ToString() => Name;
}
