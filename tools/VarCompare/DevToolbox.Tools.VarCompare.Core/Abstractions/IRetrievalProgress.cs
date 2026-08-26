namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Rend compte de l'avancement pendant la lecture des groupes, pour qu'une reprise ressemble à une reprise
/// et non à un blocage. Implémenté dans la couche Spectre.
/// </summary>
/// <remarks>
/// <para>
/// Ce port ne dit rien des reprises internes du client HTTP. Elles surviennent sous la passerelle, là où
/// aucun groupe n'est identifiable, et c'est <c>IRetryObserver</c> qui les annonce — une fois pour tous les
/// outils, plutôt qu'une fois par outil.
/// </para>
/// <para>
/// Les implémentations sont appelées depuis plusieurs fils d'exécution, la lecture des groupes étant
/// parallèle, et doivent donc sérialiser leurs écritures.
/// </para>
/// </remarks>
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

    /// <summary>Cesse de rendre compte.</summary>
    void Complete();
}
