using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Donne à un sous-arbre une empreinte stable, sur laquelle on peut regrouper.</summary>
public interface IFingerprinter
{
    /// <summary>Empreinte un sous-arbre.</summary>
    /// <param name="node">Le nœud.</param>
    /// <param name="profile">Jusqu'où la comparaison va.</param>
    /// <param name="version">La version déclarée par le document.</param>
    /// <returns>L'empreinte, en hexadécimal minuscule.</returns>
    string Fingerprint(SpecNode node, EquivalenceProfile profile, OpenApiVersion version);
}
