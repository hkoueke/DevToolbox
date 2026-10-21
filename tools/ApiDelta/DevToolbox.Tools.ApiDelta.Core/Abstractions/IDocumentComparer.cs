using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Compare deux descriptions et dit ce qui a changé.</summary>
public interface IDocumentComparer
{
    /// <summary>Compare le candidat à la production.</summary>
    /// <param name="candidate">La description candidate.</param>
    /// <param name="production">La description en production, qui sert de référence.</param>
    /// <param name="cancellationToken">Annule la comparaison.</param>
    /// <returns>Les différences, dans un ordre stable.</returns>
    Result<IReadOnlyList<Difference>> Compare(
        SpecDocument candidate,
        SpecDocument production,
        CancellationToken cancellationToken);
}
