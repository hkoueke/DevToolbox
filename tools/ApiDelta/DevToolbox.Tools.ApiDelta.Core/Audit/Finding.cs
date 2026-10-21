using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce qu'une règle a constaté, et où.</summary>
/// <param name="RuleId">L'identifiant de la règle, qui fait partie de la sortie et ne change pas.</param>
/// <param name="Severity">Combien cela importe.</param>
/// <param name="Safety">Ce que l'outil peut prouver au sujet du remède.</param>
/// <param name="Message">Ce qu'il y a à dire, déjà lisible.</param>
/// <param name="Location">L'emplacement principal.</param>
/// <param name="RecommendedAction">Ce qu'il est recommandé de faire.</param>
/// <remarks>
/// <see cref="AutofixAvailable"/> n'est pas un champ qu'on renseigne, c'est une conséquence. Un constat ne
/// peut pas se déclarer applicable automatiquement : il l'est si et seulement si son remède est prouvé sûr
/// et que l'équivalence a été établie sous le profil le plus exigeant. Rendre cela structurel évite qu'une
/// règle, un jour, affirme l'inverse par distraction.
/// </remarks>
public sealed record Finding(
    string RuleId,
    FindingSeverity Severity,
    SafetyClass Safety,
    string Message,
    SpecPointer Location,
    string RecommendedAction)
{
    /// <summary>Les autres emplacements du même groupe.</summary>
    public IReadOnlyList<SpecPointer> Locations { get; init; } = [];

    /// <summary>Les octets qu'un remède ferait gagner, quand l'économie est le sujet.</summary>
    public long? EstimatedBytesSaved { get; init; }

    /// <summary>Le nom de composant suggéré, déjà assaini.</summary>
    public string? SuggestedComponentName { get; init; }

    /// <summary>
    /// Indique si l'équivalence a été établie sous le profil qui autorise une application automatique.
    /// </summary>
    public bool StrictEquivalenceEstablished { get; init; }

    /// <summary>Sur quoi le constat repose.</summary>
    public FindingEvidence? Evidence { get; init; }

    /// <summary>Indique si un remède automatique existe.</summary>
    public bool AutofixAvailable => Safety == SafetyClass.Safe && StrictEquivalenceEstablished;

    /// <summary>La forme lisible de l'emplacement principal.</summary>
    public string Display => Location.Display;
}
