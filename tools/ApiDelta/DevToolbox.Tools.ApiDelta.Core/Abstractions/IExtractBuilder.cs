using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Construit une description autonome des seules opérations retenues.</summary>
public interface IExtractBuilder
{
    /// <summary>Monte l'extrait.</summary>
    /// <param name="source">La description dont il est tiré.</param>
    /// <param name="request">Ce que le développeur a retenu.</param>
    /// <param name="cancellationToken">Annule la construction.</param>
    /// <returns>L'extrait monté, ou de quoi expliquer son refus.</returns>
    Result<BuiltExtract> Build(
        SpecDocument source,
        ExtractRequest request,
        CancellationToken cancellationToken);
}
