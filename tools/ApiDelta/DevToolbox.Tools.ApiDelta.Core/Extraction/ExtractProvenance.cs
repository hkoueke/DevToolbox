namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>D'où vient un extrait.</summary>
/// <param name="SourcePath">Le fichier dont il est tiré.</param>
/// <param name="SourceHash">L'empreinte de ce fichier au moment de la lecture.</param>
/// <param name="ExtractedAt">Quand.</param>
/// <param name="Selectors">Les opérations retenues.</param>
/// <remarks>
/// Une description réduite à trois routes est indiscernable de la description d'une interface qui en a
/// trois, et la différence compte la première fois que l'une est relue ou publiée dans la croyance
/// qu'elle est complète.
/// </remarks>
public sealed record ExtractProvenance(
    string SourcePath,
    string SourceHash,
    DateTimeOffset ExtractedAt,
    IReadOnlyList<OperationSelector> Selectors);
