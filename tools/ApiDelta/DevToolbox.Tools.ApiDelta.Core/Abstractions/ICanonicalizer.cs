using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Réduit un sous-arbre à ce qu'il dit, sous un profil donné.</summary>
public interface ICanonicalizer
{
    /// <summary>Canonise un sous-arbre.</summary>
    /// <param name="node">Le nœud, avec tout ce qu'il contient.</param>
    /// <param name="profile">Jusqu'où la comparaison va.</param>
    /// <param name="version">La version déclarée par le document.</param>
    /// <returns>Les octets canoniques, en UTF-8.</returns>
    ReadOnlyMemory<byte> Canonicalize(SpecNode node, EquivalenceProfile profile, OpenApiVersion version);

    /// <summary>Canonise un sous-arbre, sous une forme lisible.</summary>
    /// <param name="node">Le nœud, avec tout ce qu'il contient.</param>
    /// <param name="profile">Jusqu'où la comparaison va.</param>
    /// <param name="version">La version déclarée par le document.</param>
    /// <returns>La forme canonique.</returns>
    string Text(SpecNode node, EquivalenceProfile profile, OpenApiVersion version);
}
