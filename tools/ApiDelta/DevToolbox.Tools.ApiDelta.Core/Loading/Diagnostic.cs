using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Loading;

/// <summary>Ce que la lecture a trouvé à dire, et où.</summary>
/// <param name="Code">L'identifiant stable, pris dans <see cref="DiagnosticCodes"/>.</param>
/// <param name="Severity">La gravité.</param>
/// <param name="Message">Un message déjà affichable tel quel.</param>
/// <param name="Location">Où le problème se trouve, quand il a un emplacement.</param>
/// <param name="Position">À quelle ligne et quelle colonne, quand l'analyseur l'a donnée.</param>
/// <param name="Related">
/// Les autres emplacements qui comptent : les deux moitiés d'une clé en double, chacun des usages d'une
/// référence non résolue. Nommer un seul emplacement obligerait le lecteur à chercher les autres.
/// </param>
public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    SpecPointer? Location = null,
    SourcePosition? Position = null,
    IReadOnlyList<SourcePosition>? Related = null)
{
    /// <summary>Les autres emplacements qui comptent, jamais nuls à la lecture.</summary>
    public IReadOnlyList<SourcePosition> Related { get; init; } = Related ?? [];

    /// <summary>Indique si ce diagnostic empêche de publier quoi que ce soit.</summary>
    public bool IsBlocking => Severity == DiagnosticSeverity.Error;
}
