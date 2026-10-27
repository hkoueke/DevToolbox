using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Ce que le contrôle d'une adresse a établi.</summary>
/// <param name="Refusal">Le refus, ou <see langword="null"/> si l'adresse est atteignable.</param>
/// <param name="Resolved">Les adresses auxquelles l'hôte s'est résolu.</param>
/// <remarks>
/// Les adresses résolues sont rendues <em>même quand le contrôle passe</em>, et c'est délibéré : le
/// développeur a le droit de savoir où son outil est allé, et la question « à quoi ce nom s'est-il
/// résolu » ne se pose jamais plus utilement qu'après coup.
/// </remarks>
public sealed record AddressCheck(Diagnostic? Refusal, IReadOnlyList<string> Resolved)
{
    /// <summary>Indique que l'adresse peut être atteinte.</summary>
    public bool IsAllowed => Refusal is null;

    /// <summary>Un refus, sans adresse résolue.</summary>
    /// <param name="refusal">Sa raison, déjà affichable.</param>
    /// <returns>Le contrôle refusé.</returns>
    public static AddressCheck Refused(Diagnostic refusal) => new(refusal, []);
}
