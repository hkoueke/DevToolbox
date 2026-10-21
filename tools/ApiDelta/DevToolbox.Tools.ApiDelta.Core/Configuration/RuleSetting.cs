using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Ce qu'une équipe a décidé au sujet d'une règle.</summary>
/// <param name="Enabled">La règle produit des constats.</param>
/// <param name="Severity">La gravité à employer, ou celle de la règle quand rien n'est dit.</param>
/// <param name="MinimumOccurrences">
/// Le nombre d'occurrences en deçà duquel une répétition ne vaut pas d'être signalée.
/// </param>
/// <remarks>
/// La gravité ne peut pas changer la classe de sûreté. Une équipe peut décider qu'un constat ne l'inquiète
/// pas ; elle ne peut pas décider qu'une transformation est prouvée sûre.
/// </remarks>
public sealed record RuleSetting(
    bool Enabled = true,
    FindingSeverity? Severity = null,
    int? MinimumOccurrences = null)
{
    /// <summary>Le réglage par défaut : la règle s'applique telle qu'elle est écrite.</summary>
    public static RuleSetting Default { get; } = new();
}
