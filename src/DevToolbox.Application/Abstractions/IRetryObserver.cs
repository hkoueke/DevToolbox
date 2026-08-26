namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Reçoit les reprises que le pipeline de résilience effectue de lui-même, afin qu'un appel lent se donne à
/// voir comme une reprise en cours et non comme un blocage.
/// </summary>
/// <remarks>
/// <para>
/// Ce port existe parce que les reprises se produisent <em>sous</em> le client HTTP, là où aucun outil ne
/// peut les observer. L'infrastructure les signale, la présentation les annonce, et aucun outil n'a besoin
/// de s'en occuper : un second outil hérite donc de l'annonce sans rien enregistrer.
/// </para>
/// <para>
/// Aucune méthode ne porte d'identité de ressource : une rafale de lectures parallèles rendrait un nom de
/// groupe trompeur plutôt qu'utile.
/// </para>
/// </remarks>
public interface IRetryObserver
{
    /// <summary>Signale qu'une tentative va être rejouée après un délai.</summary>
    /// <param name="attempt">De quelle reprise il s'agit, en comptant à partir de un.</param>
    /// <param name="maxAttempts">Combien de reprises le pipeline fera au plus.</param>
    /// <param name="delay">Combien de temps le pipeline patiente avant de rejouer.</param>
    void Retrying(int attempt, int maxAttempts, TimeSpan delay);

    /// <summary>Signale que le service limite le débit et que l'outil patiente comme demandé.</summary>
    /// <param name="retryAfter">Combien de temps le pipeline patiente, selon ce que le serveur a demandé.</param>
    void Throttled(TimeSpan retryAfter);
}
