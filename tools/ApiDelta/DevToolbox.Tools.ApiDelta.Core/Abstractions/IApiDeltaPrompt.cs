using DevToolbox.Domain.Results;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Ce que l'outil demande au développeur.</summary>
/// <remarks>
/// Un port, et non un appel direct à Spectre : c'est ce qui permet d'éprouver chaque étape sans terminal.
/// <para>
/// Les trois confirmations sont distinctes et non un seul « êtes-vous sûr » paramétré, parce qu'elles ne
/// protègent pas de la même chose. Écrire dans un dossier qui n'existe pas encore se défait en supprimant
/// le dossier ; remplacer un artefact se défait en relançant ; réécrire le fichier en service ne se défait
/// pas. Une invite unique les aurait rendues interchangeables à l'œil du lecteur.
/// </para>
/// </remarks>
public interface IApiDeltaPrompt
{
    /// <summary>Demande ce qu'il faut lire, et ce qu'il faut en faire.</summary>
    /// <param name="resumed">La sélection d'une exécution reprise, quand il y en a une.</param>
    /// <param name="cancellationToken">Annule l'invite.</param>
    /// <returns>La sélection, ou un échec si le développeur renonce.</returns>
    Result<RunSelection> AskForSelection(RunSelection? resumed, CancellationToken cancellationToken);

    /// <summary>Demande confirmation avant de créer le dossier où écrire.</summary>
    /// <param name="directory">Le dossier qui n'existe pas encore.</param>
    /// <returns><see langword="true"/> si le développeur confirme.</returns>
    bool ConfirmCreateDirectory(string directory);

    /// <summary>Demande confirmation avant de remplacer des artefacts déjà présents.</summary>
    /// <param name="fileNames">Les fichiers qui seraient remplacés.</param>
    /// <returns><see langword="true"/> si le développeur confirme.</returns>
    /// <remarks>
    /// Une fois, et la liste entière : trois invites pour trois rapports seraient une cérémonie, et une
    /// cérémonie s'expédie sans la lire.
    /// </remarks>
    bool ConfirmReplace(IReadOnlyList<string> fileNames);

    /// <summary>Demande confirmation avant de réécrire la description de production sur place.</summary>
    /// <param name="path">Le fichier visé, nommé dans l'invite.</param>
    /// <param name="backupPath">La sauvegarde qui sera créée, ou vide si elle a été refusée.</param>
    /// <returns><see langword="true"/> si le développeur confirme.</returns>
    /// <remarks>
    /// La seule invite de cet outil dont la réponse « oui » n'est pas réversible en supprimant un
    /// fichier, et elle le dit.
    /// </remarks>
    bool ConfirmInPlaceWrite(string path, string backupPath);
}
