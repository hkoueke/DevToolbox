using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Lit un contrat WSDL et le graphe de documents qu'il atteint.</summary>
/// <remarks>
/// Interdit par contrat, et ce ne sont pas des options que la politique peut accorder : aucune
/// implémentation ne traite une définition de type de document, ne résout une entité externe, ni n'ouvre
/// un chemin hors de la racine de confinement. Interdire les deux premières ferme XXE et l'expansion
/// d'entités du même geste, et un réglage capable de les rétablir serait lui-même la vulnérabilité.
/// </remarks>
public interface IWsdlReader
{
    /// <summary>Lit un contrat depuis le disque.</summary>
    /// <param name="path">Le document d'entrée.</param>
    /// <param name="limits">Le budget qu'elle ne peut pas dépasser.</param>
    /// <param name="policy">Ce qu'elle s'autorise à résoudre.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <param name="retrieved">
    /// D'où vient chaque document, quand ils ont été récupérés puis matérialisés.
    /// </param>
    /// <returns>Le contrat, ou de quoi expliquer son absence.</returns>
    /// <remarks>
    /// La correspondance est ce qui fait qu'un graphe récupéré se lit comme un graphe donné par chemin :
    /// un import relatif s'y résout contre l'adresse du document qui l'écrit, puis redevient un fichier.
    /// Sans elle, matérialiser changerait le sens des imports.
    /// </remarks>
    Task<WsdlReadResult> ReadAsync(
        string path,
        WsdlLimits limits,
        ImportPolicy policy,
        CancellationToken cancellationToken,
        RetrievalMap? retrieved = null);

    /// <summary>Lit un contrat depuis des octets déjà en main.</summary>
    /// <param name="content">Les octets du document d'entrée.</param>
    /// <param name="name">Le nom à employer dans les diagnostics.</param>
    /// <param name="limits">Le budget.</param>
    /// <param name="policy">La politique.</param>
    /// <returns>Le contrat, ou de quoi expliquer son absence.</returns>
    /// <remarks>
    /// Sans entrée-sortie, donc sans attente. Son emploi n'est pas une commodité : un essai doit pouvoir
    /// soumettre un document hostile sans toucher au système de fichiers, et le lecteur doit pouvoir
    /// relire un document matérialisé qu'il vient lui-même d'écrire.
    /// </remarks>
    WsdlReadResult ReadBytes(
        ReadOnlyMemory<byte> content, string name, WsdlLimits limits, ImportPolicy policy);

    /// <summary>Indique que ce document est un contrat WSDL.</summary>
    /// <param name="path">Le document.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Vrai si sa racine est un « wsdl:definitions ».</returns>
    /// <remarks>
    /// D'après le contenu, et non d'après l'extension. Un contrat servi sous « .xml » est courant, et un
    /// « .wsdl » peut contenir n'importe quoi : choisir la façon de lire un document d'après son nom
    /// revient à croire ce que le nom affirme plutôt que ce que le document est.
    /// </remarks>
    Task<bool> IsWsdlAsync(string path, CancellationToken cancellationToken);
}
