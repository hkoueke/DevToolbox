namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Rend compte de l'avancement pendant la lecture des groupes, pour qu'une reprise ressemble à une reprise
/// et non à un blocage. Implémenté dans la couche Spectre.
/// </summary>
public interface IRetrievalProgress
{
    /// <summary>Commence à rendre compte.</summary>
    /// <param name="total">Combien de groupes l'exécution requiert au total.</param>
    /// <param name="alreadyRetrieved">Combien ont déjà été lus plus tôt dans cette session.</param>
    void Begin(int total, int alreadyRetrieved);

    /// <summary>Signale qu'un groupe est en cours de lecture.</summary>
    /// <param name="groupName">Le nom du groupe.</param>
    void Retrieving(string groupName);

    /// <summary>Signale qu'un groupe a été lu.</summary>
    /// <param name="groupName">Le nom du groupe.</param>
    void Retrieved(string groupName);

    /// <summary>Signale qu'un groupe n'a pas pu être lu.</summary>
    /// <param name="groupName">Le nom du groupe.</param>
    /// <param name="reason">Un message déjà affichable tel quel.</param>
    void Failed(string groupName, string reason);

    /// <summary>Signale qu'une tentative est rejouée, pour qu'un appel lent ne paraisse pas figé.</summary>
    /// <param name="groupName">Le nom du groupe.</param>
    /// <param name="attempt">De quelle tentative il s'agit, en comptant à partir de un.</param>
    /// <param name="maxAttempts">Combien de tentatives le pipeline fera au total.</param>
    void Retrying(string groupName, int attempt, int maxAttempts);

    /// <summary>Signale que le service limite le débit et que l'outil patiente comme demandé.</summary>
    /// <param name="retryAfter">Combien de temps le serveur a demandé d'attendre.</param>
    void Throttled(TimeSpan retryAfter);

    /// <summary>Cesse de rendre compte.</summary>
    void Complete();
}
