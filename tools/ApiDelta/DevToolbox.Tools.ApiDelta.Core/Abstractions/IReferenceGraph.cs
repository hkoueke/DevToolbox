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
    /// <param name="limits">Le budget, dont le nombre de références que ce document peut porter.</param>
    /// <param name="diagnostics">Reçoit ce qu'il y a à dire : non résolues, interdites, cycles illégaux.</param>
    /// <returns>Le graphe.</returns>
    /// <remarks>
    /// Le budget est exigé, et non facultatif : un paramètre qu'un appelant peut omettre est la façon
    /// dont « limits.maxReferences » est resté déclaré sans être appliqué. Trois appelants le passent,
    /// chacun depuis la configuration qu'il a lue.
    /// </remarks>
    ReferenceGraphResult Resolve(
        SpecDocument document,
        ReferencePolicy policy,
        LoadLimits limits,
        ICollection<Diagnostic> diagnostics);
}
