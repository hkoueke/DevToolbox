using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Résout les références d'un document.</summary>
public interface IReferenceGraph
{
    /// <summary>Parcourt les références du document.</summary>
    /// <param name="document">Le document lu.</param>
    /// <param name="policy">Jusqu'où une référence a le droit d'aller.</param>
    /// <param name="diagnostics">Reçoit ce qu'il y a à dire : non résolues, interdites, cycles illégaux.</param>
    /// <returns>Le graphe.</returns>
    ReferenceGraphResult Resolve(
        SpecDocument document,
        ReferencePolicy policy,
        ICollection<Diagnostic> diagnostics);
}
