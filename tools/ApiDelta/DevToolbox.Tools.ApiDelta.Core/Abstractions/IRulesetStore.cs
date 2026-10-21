using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Lit un jeu de règles, ou explique pourquoi il n'est pas applicable.</summary>
/// <remarks>
/// Jamais de repli silencieux sur les valeurs par défaut. Un jeu de règles ignoré sans bruit change ce
/// que l'audit rapporte sans le dire — et c'est la façon dont une équipe en vient à croire qu'une règle
/// est éteinte alors qu'elle est allumée, ou l'inverse.
/// </remarks>
public interface IRulesetStore
{
    /// <summary>Lit un jeu de règles.</summary>
    /// <param name="path">Le fichier.</param>
    /// <param name="defaults">Les valeurs livrées, sur lesquelles le fichier se superpose.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Le jeu de règles, ou de quoi expliquer son refus.</returns>
    Task<RulesetReadResult> ReadAsync(
        string path,
        Ruleset defaults,
        CancellationToken cancellationToken);

    /// <summary>Cherche un jeu de règles nommé par convention à côté d'une description.</summary>
    /// <param name="descriptionPath">Le fichier en service.</param>
    /// <returns>Le chemin trouvé, ou <see langword="null"/>.</returns>
    string? Discover(string descriptionPath);
}
