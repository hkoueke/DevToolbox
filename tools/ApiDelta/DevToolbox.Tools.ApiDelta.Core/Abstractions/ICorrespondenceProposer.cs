using DevToolbox.Tools.ApiDelta.Core.Correspondence;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Propose un appariement entre les opérations d'un contrat et les routes d'une production.</summary>
/// <remarks>
/// Propose, et ne décide pas. Ce que l'implémentation rend n'est jamais confirmé : la confirmation
/// appartient au développeur, et aucune comparaison n'a lieu avant qu'il l'ait donnée.
/// <para>
/// Pur par contrat : deux appels sur les mêmes entrées rendent la même proposition. Une proposition qui
/// changerait d'une séance à l'autre ferait qu'une reprise ne porterait plus sur la même chose que ce que
/// le développeur avait confirmé.
/// </para>
/// </remarks>
public interface ICorrespondenceProposer
{
    /// <summary>Propose un appariement.</summary>
    /// <param name="operations">Les noms des opérations du contrat.</param>
    /// <param name="routes">Les routes de la production, méthode et chemin.</param>
    /// <param name="declared">Les appariements que le développeur a déclarés, qui priment.</param>
    /// <returns>La correspondance proposée, non confirmée.</returns>
    OperationCorrespondence Propose(
        IReadOnlyList<string> operations,
        IReadOnlyList<string> routes,
        IReadOnlyDictionary<string, string>? declared = null);
}
