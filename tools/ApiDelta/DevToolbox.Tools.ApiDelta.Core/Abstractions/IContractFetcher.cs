using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Récupère un contrat depuis une adresse, et le matérialise avant de le rendre.</summary>
/// <remarks>
/// Le seul appel sortant de cette tranche, et le premier de la boîte. Il rend un contrat dont les
/// documents sont des <em>fichiers</em> : matérialiser est ce qui permet à une entrée nouvelle d'obéir aux
/// règles déjà en place — un chemin, une taille, une empreinte — au lieu d'exiger des exceptions.
/// <para>
/// Interdit par contrat : aucune implémentation n'acquiert ni n'envoie d'identifiant, ne suit un
/// <c>soap:address</c> que personne n'a demandé, ni ne récupère sans les contrôles de schéma et d'adresse.
/// Récupérer un contrat est une lecture ; les méthodes d'écriture HTTP sont d'ailleurs bannies partout
/// dans ce dépôt.
/// </para>
/// </remarks>
public interface IContractFetcher
{
    /// <summary>Récupère le graphe que l'adresse d'entrée atteint, et l'écrit sur le disque.</summary>
    /// <param name="entry">L'adresse que le développeur a donnée.</param>
    /// <param name="outputDirectory">Où matérialiser les documents.</param>
    /// <param name="limits">Le budget de la récupération et de la lecture.</param>
    /// <param name="policy">Ce qu'elle s'autorise à atteindre.</param>
    /// <param name="cancellationToken">Annule la récupération.</param>
    /// <returns>Le contrat matérialisé, ou un échec déjà affichable.</returns>
    Task<Result<WsdlContract>> FetchAsync(
        Uri entry,
        string outputDirectory,
        WsdlLimits limits,
        ImportPolicy policy,
        CancellationToken cancellationToken);
}
