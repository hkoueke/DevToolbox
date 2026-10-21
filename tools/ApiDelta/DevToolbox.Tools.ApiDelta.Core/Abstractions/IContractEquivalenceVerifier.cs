using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Optimization;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Établit qu'une copie optimisée dit au contrat la même chose que l'original.</summary>
/// <remarks>
/// Sur les octets qui seront écrits, et non sur l'arbre édité. La différence est le cœur de la garantie :
/// l'arbre édité est ce que l'outil croit avoir produit, les octets sont ce qu'un appelant lira. Vérifier
/// le premier laisserait passer exactement les défauts que l'écriture introduit — un scalaire requoté, une
/// clé réordonnée au mauvais endroit, un bloc mal indenté.
/// </remarks>
public interface IContractEquivalenceVerifier
{
    /// <summary>Vérifie une copie optimisée.</summary>
    /// <param name="serialized">Les octets qui seront écrits.</param>
    /// <param name="original">La description d'origine.</param>
    /// <param name="ruleset">Ce que l'équipe a décidé, dont la politique de référence.</param>
    /// <returns>Ce que la vérification a pu établir.</returns>
    VerificationResult Verify(
        ReadOnlyMemory<byte> serialized,
        SpecDocument original,
        Ruleset ruleset);
}
