using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce sur quoi repose une sélection faite contre un contrat de service.</summary>
/// <param name="Derivation">D'où chaque élément projeté vient, et ce que la projection n'a pas porté.</param>
/// <param name="Correspondence">L'appariement entre les opérations et les routes de la production.</param>
/// <param name="Coverage">Ce que la production couvre du contrat.</param>
/// <param name="Projection">Ce que la projection a produit et laissé derrière, pour les rapports.</param>
/// <remarks>
/// Les trois voyagent ensemble parce qu'elles répondent ensemble à une seule question : sur quelle base
/// le développeur choisit-il. La projection dit ce que la traduction valait, la correspondance dit si la
/// paire vient de lui ou de l'outil, la couverture dit si la production répond déjà. Lui montrer l'une
/// sans les autres le laisserait choisir en croyant en savoir plus qu'il n'en sait.
/// <para>
/// Facultatif partout : une description candidate OpenAPI n'a pas de base de ce genre, et son absence
/// est ce qui distingue les deux chemins sans qu'aucun drapeau ne le dise.
/// </para>
/// </remarks>
public sealed record WsdlFooting(
    DerivationIndex Derivation,
    OperationCorrespondence Correspondence,
    CoverageReport Coverage,
    ProjectionResult? Projection = null)
{
    /// <summary>Une base qui ne sait rien : le candidat n'est pas un contrat de service.</summary>
    public static WsdlFooting None { get; } = new(
        DerivationIndex.Empty, OperationCorrespondence.None, CoverageReport.NotEstablished);

    /// <summary>L'état de couverture d'une route de la production, s'il est établi.</summary>
    /// <param name="route">La route, « MÉTHODE /chemin ».</param>
    /// <returns>L'état, ou rien si la couverture n'a pas été établie pour elle.</returns>
    public CoverageState? StateOf(string route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return Entry(route) is { } entry ? entry.State : null;
    }

    /// <summary>L'appariement dont une route dépend, s'il y en a un.</summary>
    /// <param name="route">La route, « MÉTHODE /chemin ».</param>
    /// <returns>L'entrée de correspondance, ou rien.</returns>
    public CorrespondenceEntry? PairedWith(string route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return Entry(route) is { } entry ? Correspondence.For(entry.Operation) : null;
    }

    /// <summary>L'entrée de couverture d'une route, par l'opération dont elle est dérivée.</summary>
    /// <remarks>
    /// Retrouvée par l'itinéraire dérivé, et non par la route que la correspondance désigne : une
    /// opération absente de la production n'a pas de route à désigner, et ce sont précisément celles-là
    /// que le développeur est en train d'insérer.
    /// </remarks>
    private CoverageEntry? Entry(string route)
    {
        foreach (CoverageEntry entry in Coverage.Entries)
        {
            if (string.Equals(entry.Route, route, StringComparison.Ordinal))
            {
                return entry;
            }

            if (Derivation.Derivations.TryGetValue(entry.Operation, out DerivedRoute? derived)
                && string.Equals(derived.Display, route, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }
}
