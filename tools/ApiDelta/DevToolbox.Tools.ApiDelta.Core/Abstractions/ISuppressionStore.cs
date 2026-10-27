using DevToolbox.Tools.ApiDelta.Core.Configuration;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Lit un fichier de sourdines, ou explique pourquoi il n'est pas applicable.</summary>
public interface ISuppressionStore
{
    /// <summary>Lit les sourdines.</summary>
    /// <param name="path">Le fichier.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Les sourdines, ou de quoi expliquer leur refus.</returns>
    Task<SuppressionReadResult> ReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>Cherche un fichier de sourdines nommé par convention à côté d'une description.</summary>
    /// <param name="descriptionPath">Le fichier de production.</param>
    /// <returns>Le chemin trouvé, ou <see langword="null"/>.</returns>
    string? Discover(string descriptionPath);
}
