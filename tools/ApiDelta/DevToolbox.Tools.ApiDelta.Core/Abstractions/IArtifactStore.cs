using DevToolbox.Domain.Results;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Écrit les artefacts d'une exécution.</summary>
/// <remarks>
/// Le seul port de l'outil qui écrive quelque part. Tout le reste produit des octets : c'est ce qui permet
/// de vérifier un artefact avant qu'il n'existe.
/// </remarks>
public interface IArtifactStore
{
    /// <summary>
    /// Prépare le dossier de sortie : le crée si besoin, et fait confirmer les remplacements.
    /// </summary>
    /// <param name="fileNames">Les artefacts que l'exécution s'apprête à écrire.</param>
    /// <param name="selection">La sélection, qui porte le dossier de sortie.</param>
    /// <param name="cancellationToken">Annule la préparation.</param>
    /// <returns>Un succès si l'écriture peut commencer, un échec déjà affichable sinon.</returns>
    /// <remarks>
    /// Avant la première écriture, et une seule fois : le développeur voit la liste complète de ce qui
    /// sera remplacé, au lieu d'une suite d'invites dont chacune ne montre qu'une pièce. Demander après
    /// avoir écrit le premier fichier aurait aussi laissé un dossier à moitié rempli derrière un refus.
    /// </remarks>
    Task<Result> PrepareAsync(
        IReadOnlyList<string> fileNames,
        RunSelection selection,
        CancellationToken cancellationToken);

    /// <summary>Publie un artefact dans le dossier de sortie.</summary>
    /// <param name="kind">La sorte d'artefact.</param>
    /// <param name="fileName">Son nom de fichier.</param>
    /// <param name="content">Son contenu, déjà complet.</param>
    /// <param name="selection">La sélection, qui porte le dossier de sortie.</param>
    /// <param name="cancellationToken">Annule l'écriture.</param>
    /// <returns>L'artefact écrit, ou un échec déjà affichable.</returns>
    Task<Result<Artifact>> PublishAsync(
        ArtifactKind kind,
        string fileName,
        ReadOnlyMemory<byte> content,
        RunSelection selection,
        CancellationToken cancellationToken);
}
