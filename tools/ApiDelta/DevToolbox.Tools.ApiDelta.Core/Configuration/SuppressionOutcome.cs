using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Ce que les sourdines ont changé, et ce qu'elles n'ont pas trouvé.</summary>
/// <param name="Findings">Les constats qui subsistent.</param>
/// <param name="Suppressed">Les constats tus, avec la décision qui les tait.</param>
/// <param name="Expired">Les sourdines échues, qui n'ont pas été honorées.</param>
/// <param name="Unmatched">Les sourdines qui n'ont apparié aucun constat.</param>
/// <remarks>
/// Les trois dernières listes sont le rendu de comptes. Une sourdine est une décision prise une fois et
/// relue rarement : sans ces listes, un fichier de sourdines ne se vérifierait qu'en le comparant à la
/// main au document, c'est-à-dire jamais.
/// </remarks>
public sealed record SuppressionOutcome(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<SuppressedFinding> Suppressed,
    IReadOnlyList<Suppression> Expired,
    IReadOnlyList<Suppression> Unmatched)
{
    /// <summary>Un examen sans aucune sourdine.</summary>
    public static SuppressionOutcome None { get; } = new([], [], [], []);

    /// <summary>Indique s'il y a quelque chose à dire au sujet des sourdines.</summary>
    public bool HasAnythingToSay =>
        Suppressed.Count > 0 || Expired.Count > 0 || Unmatched.Count > 0;
}
