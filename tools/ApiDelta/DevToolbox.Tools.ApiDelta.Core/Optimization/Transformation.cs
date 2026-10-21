using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Une transformation, proposée et rien de plus tant qu'elle n'a pas été appliquée.</summary>
/// <param name="RuleId">La règle d'optimisation qui la commande.</param>
/// <param name="Safety">Ce que l'outil peut prouver à son sujet.</param>
/// <param name="Origins">Ce qu'elle remplace.</param>
/// <param name="Destination">Le composant qu'elle créerait, quand elle en crée un.</param>
/// <param name="Evidence">L'empreinte qui la justifie, quand une empreinte la justifie.</param>
/// <param name="Reversal">Comment la défaire.</param>
/// <param name="State">Où elle en est.</param>
/// <remarks>
/// L'état naît à <see cref="TransformationState.Proposed"/> et c'est volontaire : le plan est un devis,
/// non un travail commencé. Rien dans ce type ne tient le contenu transformé — ce qui fait qu'un plan ne
/// peut pas, même par accident, servir de véhicule à une écriture.
/// </remarks>
public sealed record Transformation(
    string RuleId,
    SafetyClass Safety,
    IReadOnlyList<SpecPointer> Origins,
    SpecPointer? Destination,
    string? Evidence,
    ReversalDescription Reversal,
    TransformationState State)
{
    /// <summary>Ce qu'elle fait, dit à un lecteur.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Le constat dont elle procède.</summary>
    public string? FromRuleId { get; init; }

    /// <summary>Ce qu'elle ferait gagner.</summary>
    public long EstimatedBytesSaved { get; init; }

    /// <summary>Le nombre d'occurrences à partir duquel elle commence à gagner.</summary>
    public int? BreakEvenOccurrences { get; init; }

    /// <summary>Pourquoi une relecture est nécessaire, quand elle l'est.</summary>
    public string? ReviewReason { get; init; }

    /// <summary>Indique si l'outil peut l'appliquer sans qu'on le lui permette nommément.</summary>
    /// <remarks>
    /// Calculé, non renseigné : une transformation ne peut pas se déclarer applicable. Elle l'est si et
    /// seulement si sa sûreté est établie et si l'égalité qui la justifie a été prouvée sous le profil le
    /// plus exigeant.
    /// </remarks>
    public bool ApplicableWithoutPermission =>
        Safety == SafetyClass.Safe && Evidence is not null;
}
