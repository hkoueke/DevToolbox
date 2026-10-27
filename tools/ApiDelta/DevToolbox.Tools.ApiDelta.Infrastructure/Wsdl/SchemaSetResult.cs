using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Ce que la compilation du jeu de schémas a produit.</summary>
/// <param name="Types">Les types compilés, par nom qualifié.</param>
/// <param name="Elements">Les éléments globaux et le type de chacun.</param>
/// <param name="Documents">Les schémas effectivement lus.</param>
/// <param name="Unresolved">Les imports que la politique n'a pas laissé résoudre.</param>
internal sealed record SchemaSetResult(
    IReadOnlyDictionary<string, XsdType> Types,
    IReadOnlyDictionary<string, string> Elements,
    IReadOnlyList<WsdlDocument> Documents,
    IReadOnlyList<UnresolvedImport> Unresolved);
