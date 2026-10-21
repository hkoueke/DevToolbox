using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Réécrit un document en ne touchant que les endroits désignés.</summary>
/// <remarks>
/// C'est le troisième émetteur, et il existe parce que ni l'un ni l'autre des deux premiers ne peut tenir
/// la promesse qui compte ici. L'émetteur canonique réécrit tout, donc reformaterait des milliers de
/// lignes que personne n'a demandé de changer et noierait la seule modification voulue dans un diff
/// illisible. L'émetteur fidèle reproduit tout à l'identique, donc ne peut rien changer. Celui-ci
/// réassemble les étendues inchangées autour de ce qui change — ce qui est exactement ce que
/// <see cref="IVerbatimEmitter"/> annonçait comme sa suite.
/// <para>
/// La conséquence vaut d'être dite : les commentaires, l'indentation et l'ordre des clés de la production
/// survivent partout où l'on n'a pas touché, parce qu'on y recopie les octets d'origine. C'est ce qui
/// rend le résultat relisible : le diff qu'un développeur lit est la sélection qu'il a faite.
/// </para>
/// </remarks>
public interface ISplicingEmitter
{
    /// <summary>Réécrit le document en appliquant les épissures demandées, et rien d'autre.</summary>
    /// <param name="document">Le document d'origine, avec ses octets et son arbre.</param>
    /// <param name="splices">Les changements ponctuels, dans n'importe quel ordre.</param>
    /// <param name="formatting">La mise en forme des parties neuves seulement.</param>
    /// <returns>Les octets réécrits, ou un échec déjà affichable.</returns>
    Result<ReadOnlyMemory<byte>> Splice(
        SpecDocument document,
        IReadOnlyList<Splice> splices,
        CanonicalFormatting formatting);
}
