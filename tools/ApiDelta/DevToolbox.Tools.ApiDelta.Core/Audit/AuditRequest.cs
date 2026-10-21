using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce qu'il faut pour auditer une description.</summary>
/// <param name="Document">La description auditée, seule : l'audit ne compare rien.</param>
/// <param name="Ruleset">Ce que l'équipe a décidé.</param>
/// <param name="References">Le graphe des références.</param>
/// <param name="StructuralVerdicts">Ce que la validation structurelle a trouvé à dire.</param>
/// <remarks>
/// Les deux derniers valent <see langword="null"/> quand l'analyse n'a pas eu lieu, et ce n'est pas la
/// même chose qu'une liste vide : une liste vide dit « rien à signaler », l'absence dit « on n'a pas
/// regardé ». Les règles qui en dépendent se déclarent non évaluées dans le second cas, au lieu de
/// conclure sans.
/// </remarks>
public sealed record AuditRequest(
    SpecDocument Document,
    Ruleset Ruleset,
    ReferenceGraphResult? References = null,
    IReadOnlyList<Diagnostic>? StructuralVerdicts = null);
