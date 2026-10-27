namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Le budget qu'une lecture de contrat WSDL ne peut pas dépasser.</summary>
/// <param name="MaxDocumentBytes">La taille d'un document.</param>
/// <param name="MaxNestingDepth">La profondeur d'imbrication d'un document.</param>
/// <param name="MaxImportChainDepth">La longueur d'une chaîne d'imports.</param>
/// <param name="MaxDocumentCount">Le nombre de documents d'un graphe.</param>
/// <param name="MaxEnumerationValues">Le nombre de valeurs d'une énumération.</param>
/// <param name="MaxResponseBytes">La taille maximale d'une réponse récupérée.</param>
/// <param name="FetchTimeoutSeconds">Le délai au-delà duquel une récupération est abandonnée.</param>
/// <remarks>
/// Chaque limite existe pour être <em>annoncée</em> quand elle est atteinte, et non pour faire échouer en
/// silence. La leçon vient de la tranche précédente : une chaîne de références plausible y avait fait
/// déborder la pile, sans diagnostic, sans position, sans rien dire de ce qui venait d'être lu. Un budget
/// nommé rend la même forme refusée et compréhensible.
/// <para>
/// Les défauts sont généreux. Ils sont là pour arrêter l'hostile, pas pour gêner l'inhabituel : un
/// service piloté par des listes de codes porte réellement des dizaines de milliers de valeurs
/// d'énumération.
/// </para>
/// </remarks>
public sealed record WsdlLimits(
    long MaxDocumentBytes = 16L * 1024 * 1024,
    int MaxNestingDepth = 256,
    int MaxImportChainDepth = 64,
    int MaxDocumentCount = 256,
    int MaxEnumerationValues = 100_000,
    long MaxResponseBytes = 16L * 1024 * 1024,
    int FetchTimeoutSeconds = 45)
{
    /// <summary>Le budget par défaut.</summary>
    public static WsdlLimits Default { get; } = new();
}
