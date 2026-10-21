using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>Un extrait monté, avant marque de provenance, émission et vérification.</summary>
/// <param name="Document">L'arbre de l'extrait.</param>
/// <param name="Closure">Ce qui est venu avec.</param>
/// <param name="CarriedTags">Les étiquettes employées.</param>
/// <param name="InheritedContext">Ce que chaque opération hérite.</param>
/// <remarks>
/// Distinct de <see cref="ExtractResult"/> : celui-ci est ce que le constructeur sait, celui-là ce que
/// l'exécution a établi. Les confondre obligerait le constructeur à porter une vérification qu'il n'a pas
/// faite.
/// </remarks>
public sealed record BuiltExtract(
    SpecNode Document,
    DependencyClosure Closure,
    IReadOnlyList<string> CarriedTags,
    IReadOnlyList<InheritanceRecord> InheritedContext);
