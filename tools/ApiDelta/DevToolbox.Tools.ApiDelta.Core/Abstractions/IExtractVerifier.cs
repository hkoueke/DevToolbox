using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Établit qu'un extrait dit, des opérations retenues, ce que la source en dit.</summary>
/// <remarks>
/// Sur les octets émis, comme pour l'équivalence de contrat. Vérifier l'arbre monté laisserait passer ce
/// que l'écriture introduit.
/// </remarks>
public interface IExtractVerifier
{
    /// <summary>Vérifie un extrait.</summary>
    /// <param name="serialized">Les octets qui seraient écrits.</param>
    /// <param name="source">La description dont il est tiré.</param>
    /// <param name="selectors">Les opérations retenues.</param>
    /// <param name="ruleset">Ce que l'équipe a décidé, dont la politique de référence.</param>
    /// <returns>Ce que la vérification a pu établir.</returns>
    ExtractVerification Verify(
        ReadOnlyMemory<byte> serialized,
        SpecDocument source,
        IReadOnlyList<OperationSelector> selectors,
        Ruleset ruleset);
}
