namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>Ce qu'il faut pour construire un extrait.</summary>
/// <param name="Selectors">Les opérations retenues.</param>
/// <param name="StampProvenance">Appose la marque de provenance.</param>
/// <param name="OutputName">Le nom du fichier produit.</param>
/// <remarks>
/// Invariant : une sélection vide est une erreur d'usage, refusée avant tout travail. Il n'existe pas
/// d'extrait vide — un fichier OpenAPI sans chemin ne décrit rien, et le produire sans se plaindre
/// laisserait croire que la sélection avait abouti.
/// </remarks>
public sealed record ExtractRequest(
    IReadOnlyList<OperationSelector> Selectors,
    bool StampProvenance = true,
    string OutputName = "openapi-extract.yaml")
{
    /// <summary>Indique si la demande a un objet.</summary>
    public bool HasSelection => Selectors.Count > 0;
}
