namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Le contrat tel qu'il a été lu : le graphe entier, et non le fichier d'entrée.</summary>
/// <param name="Documents">Chaque document ouvert, dans l'ordre où il l'a été.</param>
/// <param name="Operations">Les opérations, l'unité qu'une correspondance apparie.</param>
/// <param name="Types">Le jeu de schémas compilé, par nom qualifié.</param>
/// <param name="Services">Les services et leurs accès.</param>
/// <param name="Unresolved">Ce que la politique n'a pas laissé résoudre.</param>
/// <param name="ConfinementRoot">La racine à laquelle la résolution locale s'est confinée.</param>
/// <param name="Elements">
/// Les éléments globaux et le type de chacun, par nom qualifié.
/// </param>
/// <remarks>
/// L'unité est le graphe, et c'est ce qui distingue ce type de son équivalent OpenAPI : un contrat WSDL
/// vit dans plusieurs fichiers, et un rapport qui n'aurait nommé que le point d'entrée ne dirait pas ce
/// qui a été examiné.
/// <para>
/// La racine de confinement est portée parce qu'un défaut ouvert ne doit pas être silencieux : la
/// résolution locale est active par défaut, et le développeur doit pouvoir voir le périmètre que l'outil
/// s'est donné plutôt que le déduire.
/// </para>
/// </remarks>
public sealed record WsdlContract(
    IReadOnlyList<WsdlDocument> Documents,
    IReadOnlyList<WsdlOperation> Operations,
    IReadOnlyDictionary<string, XsdType> Types,
    IReadOnlyList<WsdlService> Services,
    IReadOnlyList<UnresolvedImport> Unresolved,
    string ConfinementRoot,
    IReadOnlyDictionary<string, string>? Elements = null)
{
    /// <summary>
    /// Les éléments globaux, jamais nuls.
    /// </summary>
    /// <remarks>
    /// Nécessaires parce qu'une partie de message désigne presque toujours un <em>élément</em>, et non un
    /// type : sans cette table, la projection connaîtrait les types du contrat sans savoir lequel forme
    /// le corps d'une requête, et produirait des opérations sans charge utile. Le défaut a été vu en
    /// vidant ce que la projection produisait vraiment, où les opérations sortaient vides.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Elements { get; init; } =
        Elements ?? new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Indique que le contrat a été lu en entier.
    /// </summary>
    /// <remarks>
    /// Calculé, jamais renseigné. Un contrat dont un import n'a pas été résolu ne doit pas pouvoir
    /// prétendre le contraire : c'est ce qui force les règles qui en dépendent à se déclarer non évaluées
    /// plutôt qu'à rendre un résultat propre qu'elles n'ont pas établi.
    /// </remarks>
    public bool IsComplete => Unresolved.Count == 0;

    /// <summary>Le document d'entrée : le premier ouvert.</summary>
    public WsdlDocument Entry => Documents[0];

    /// <summary>Les opérations qu'aucune liaison n'expose.</summary>
    public IEnumerable<WsdlOperation> Unreachable =>
        Operations.Where(operation => operation.IsUnreachable);
}
