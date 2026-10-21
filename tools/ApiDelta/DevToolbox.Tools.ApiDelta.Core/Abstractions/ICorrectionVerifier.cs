using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Établit qu'une production corrigée dit ce qu'on lui a demandé de dire, et rien de plus.</summary>
/// <remarks>
/// Sur les octets qui seront écrits, et non sur l'arbre modifié : vérifier l'arbre laisserait passer
/// exactement les défauts que l'écriture introduit. C'est la leçon que la copie optimisée de cet outil
/// avait déjà apprise, et il n'y avait aucune raison de la réapprendre ici.
/// </remarks>
public interface ICorrectionVerifier
{
    /// <summary>Vérifie la production corrigée.</summary>
    /// <param name="serialized">Les octets qui seraient publiés.</param>
    /// <param name="production">La production telle qu'elle a été lue.</param>
    /// <param name="built">Ce que la construction a produit : son plan et ses traces.</param>
    /// <param name="ruleset">Les limites et la politique de référence en vigueur.</param>
    /// <returns>Ce qui a pu être établi, épreuve par épreuve.</returns>
    CorrectionVerification Verify(
        ReadOnlyMemory<byte> serialized,
        SpecDocument production,
        BuiltCorrection built,
        Ruleset ruleset);
}
