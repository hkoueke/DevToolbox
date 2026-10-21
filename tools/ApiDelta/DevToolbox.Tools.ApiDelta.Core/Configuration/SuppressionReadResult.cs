using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Des sourdines lues, ou de quoi expliquer leur refus.</summary>
/// <param name="Suppressions">Les sourdines, absentes quand le fichier n'est pas applicable.</param>
/// <param name="Diagnostics">Ce qu'il y a à dire, situé dans le fichier.</param>
public sealed record SuppressionReadResult(
    IReadOnlyList<Suppression>? Suppressions,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Un refus.</summary>
    /// <param name="diagnostics">Ce qui l'explique.</param>
    /// <returns>Le résultat.</returns>
    public static SuppressionReadResult Refused(IReadOnlyList<Diagnostic> diagnostics) =>
        new(null, diagnostics);

    /// <summary>Une lecture aboutie.</summary>
    /// <param name="suppressions">Les sourdines.</param>
    /// <returns>Le résultat.</returns>
    public static SuppressionReadResult Read(IReadOnlyList<Suppression> suppressions) =>
        new(suppressions, []);

    /// <summary>Indique si le fichier a été compris en entier.</summary>
    public bool IsApplicable => Suppressions is not null;
}
