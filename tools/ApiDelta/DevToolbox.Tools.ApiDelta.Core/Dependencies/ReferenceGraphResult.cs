namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>Le graphe des références d'un document.</summary>
/// <param name="Edges">Chaque référence rencontrée, dans l'ordre de la traversée.</param>
/// <param name="Cycles">
/// Les cycles rencontrés, exprimés par la cible où la traversée s'est refermée. Légaux entre schémas :
/// un dossier qui contient un dossier est une description juste, non une erreur.
/// </param>
/// <param name="Exhausted">
/// La traversée s'est arrêtée sur un budget — chaîne trop longue ou trop de références — et n'a donc pas
/// tout vu.
/// </param>
/// <remarks>
/// Le drapeau est exigé, et non facultatif, parce que ce qu'il porte est précisément ce qu'on oublie : un
/// graphe tronqué ressemble en tout point à un graphe complet. Les deux budgets qui l'arrêtent ne
/// produisaient qu'un avertissement, que les trois lecteurs du graphe ignoraient — un avertissement
/// n'étant pas bloquant, chacun concluait depuis ce qu'il avait sous la main. Une référence morte située
/// au-delà du budget passait ainsi la vérification d'une production consolidée, et un composant
/// atteignable seulement au-delà pouvait être annoncé comme retirable sans risque.
/// </remarks>
public sealed record ReferenceGraphResult(
    IReadOnlyList<ReferenceEdge> Edges,
    IReadOnlyList<ReferenceTarget> Cycles,
    bool Exhausted)
{
    /// <summary>Indique que la traversée a tout vu, et que ce graphe se prête donc à une conclusion.</summary>
    public bool IsComplete => !Exhausted;

    /// <summary>Les références qui ne désignent rien.</summary>
    public IEnumerable<ReferenceEdge> Unresolved =>
        Edges.Where(edge => edge.Resolution == ReferenceResolution.Unresolved);

    /// <summary>Les références préservées hors du document.</summary>
    public IEnumerable<ReferenceEdge> ExternalPreserved =>
        Edges.Where(edge => edge.Resolution == ReferenceResolution.PreservedExternal);
}
