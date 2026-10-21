using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Ce que la lecture d'une description a produit : un document, ou de quoi expliquer son absence.</summary>
/// <param name="Document">
/// La description lue, ou <see langword="null"/> si elle n'a pas pu l'être fidèlement.
/// </param>
/// <param name="Diagnostics">Tout ce que la lecture a trouvé à dire, y compris en cas de succès.</param>
/// <remarks>
/// Un type dédié plutôt qu'un <c>Result&lt;T&gt;</c> : des diagnostics accompagnent aussi une lecture
/// réussie — un scalaire ambigu, du texte encodé — et les réduire à un message d'échec les perdrait.
/// </remarks>
public sealed record SpecReadResult(SpecDocument? Document, IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Indique si la description a été lue fidèlement.</summary>
    public bool IsFaithful => Document is not null;

    /// <summary>Les diagnostics qui empêchent de publier quoi que ce soit.</summary>
    public IEnumerable<Diagnostic> Blocking => Diagnostics.Where(diagnostic => diagnostic.IsBlocking);

    /// <summary>Construit l'issue d'une lecture qui a échoué.</summary>
    /// <param name="diagnostics">Ce qui l'a empêchée.</param>
    /// <returns>L'issue, sans document.</returns>
    public static SpecReadResult Failed(IReadOnlyList<Diagnostic> diagnostics) => new(null, diagnostics);
}
