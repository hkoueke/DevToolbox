namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>Le graphe des références d'un document.</summary>
/// <param name="Edges">Chaque référence rencontrée, dans l'ordre de la traversée.</param>
/// <param name="Cycles">
/// Les cycles rencontrés, exprimés par la cible où la traversée s'est refermée. Légaux entre schémas :
/// un dossier qui contient un dossier est une description juste, non une erreur.
/// </param>
public sealed record ReferenceGraphResult(
    IReadOnlyList<ReferenceEdge> Edges,
    IReadOnlyList<ReferenceTarget> Cycles)
{
    /// <summary>Les références qui ne désignent rien.</summary>
    public IEnumerable<ReferenceEdge> Unresolved =>
        Edges.Where(edge => edge.Resolution == ReferenceResolution.Unresolved);

    /// <summary>Les références préservées hors du document.</summary>
    public IEnumerable<ReferenceEdge> ExternalPreserved =>
        Edges.Where(edge => edge.Resolution == ReferenceResolution.PreservedExternal);
}
