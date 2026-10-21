using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Plusieurs écritures de la même chose, mesurées et chiffrées.</summary>
/// <param name="RuleId">La règle qui a constaté la répétition.</param>
/// <param name="Fingerprint">L'empreinte qui prouve que ses membres sont les mêmes.</param>
/// <param name="Profile">Sous quel profil cette preuve a été établie.</param>
/// <param name="Kind">La sorte de chose rassemblée.</param>
/// <param name="Tier">Jusqu'où les membres se ressemblent.</param>
/// <param name="Occurrences">Chaque écriture, avec son emplacement et sa taille.</param>
/// <param name="Savings">Ce que l'extraction ferait gagner.</param>
/// <remarks>
/// Le groupe est l'unité de décision : un en-tête identique écrit dans quatorze opérations est une
/// décision à prendre, pas quatorze. Il porte tout ce qu'il faut pour la prendre — le compte, les
/// tailles, le seuil de rentabilité et la sûreté — parce qu'une recommandation sans chiffre ne se
/// tranche pas.
/// </remarks>
public sealed record RedundancyGroup(
    string RuleId,
    string Fingerprint,
    string Profile,
    CandidateKind Kind,
    DuplicateTier Tier,
    IReadOnlyList<Occurrence> Occurrences,
    SavingsEstimate Savings)
{
    /// <summary>Les champs par lesquels les membres diffèrent, vides pour un double exact.</summary>
    public IReadOnlyList<string> DifferingFields { get; init; } = [];

    /// <summary>Le nom de composant suggéré, déjà assaini.</summary>
    public string? SuggestedComponentName { get; init; }

    /// <summary>
    /// Indique si l'équivalence a été établie sous le profil qui autorise une application automatique.
    /// </summary>
    public bool StrictEquivalenceEstablished { get; init; }

    /// <summary>Le nombre d'occurrences.</summary>
    public int Count => Occurrences.Count;

    /// <summary>L'emplacement de la première écriture.</summary>
    public SpecPointer Location => Occurrences[0].Location;

    /// <summary>Les emplacements de toutes les écritures.</summary>
    public IReadOnlyList<SpecPointer> Locations =>
        [.. Occurrences.Select(occurrence => occurrence.Location)];

    /// <summary>Ce que l'outil peut prouver au sujet du remède.</summary>
    public SafetyClass Safety => Savings.Safety;
}
