namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Ce qui gouverne une extraction.</summary>
/// <param name="StampProvenance">
/// Inscrit la provenance dans l'extrait lui-même. Actif par défaut, et c'est le seul endroit où l'outil
/// ajoute quelque chose à un document : une description réduite à trois routes ressemble trait pour trait
/// à la description d'une API qui en compte trois, et la différence compte la première fois que l'une est
/// relue ou publiée en croyant qu'elle est complète.
/// </param>
/// <param name="DefaultName">Le nom du fichier produit, à défaut d'un autre.</param>
/// <param name="CarryDocumentExtensions">
/// Emporte les extensions de fournisseur déclarées à la racine du document.
/// </param>
public sealed record ExtractSettings(
    bool StampProvenance = true,
    string DefaultName = "openapi-extract.yaml",
    bool CarryDocumentExtensions = true)
{
    /// <summary>Les réglages par défaut.</summary>
    public static ExtractSettings Default { get; } = new();
}
