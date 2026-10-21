using DevToolbox.Domain.Results;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>
/// Répond des questions sur un fichier de description sans le lire tout entier.
/// </summary>
/// <remarks>
/// Existe pour que l'étape de sélection puisse établir qu'un chemin désigne un fichier lisible, de taille
/// admissible, et en prendre l'empreinte — tout cela sans que le cœur de l'outil touche le système de
/// fichiers. L'empreinte est ce qui rend une reprise honnête : si le fichier a changé depuis, la reprise
/// est refusée au lieu de répondre avec assurance au sujet d'un document qui n'existe plus.
/// </remarks>
public interface ISpecFileProbe
{
    /// <summary>Établit qu'un chemin désigne un fichier utilisable, et en prend l'empreinte.</summary>
    /// <param name="path">Le chemin tel que le développeur l'a donné.</param>
    /// <param name="maxBytes">La taille au-delà de laquelle le fichier est refusé.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Ce que le fichier est, ou un échec déjà affichable.</returns>
    Task<Result<SpecFileFacts>> ProbeAsync(string path, long maxBytes, CancellationToken cancellationToken);
}
