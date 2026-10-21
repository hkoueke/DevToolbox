using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>Un extrait construit, et tout ce qu'il faut en dire.</summary>
/// <param name="Document">L'extrait, avant émission.</param>
/// <param name="Serialized">Les octets émis, vides si la vérification n'a pas abouti.</param>
/// <param name="Closure">Ce qui est venu avec, et pourquoi.</param>
/// <param name="CarriedTags">Les définitions d'étiquettes emportées.</param>
/// <param name="InheritedContext">Ce que chaque opération a hérité.</param>
/// <param name="DanglingExtensionReferences">
/// Les extensions emportées qui nomment un chemin ou une opération absents de l'extrait.
/// </param>
/// <param name="Provenance">D'où il vient.</param>
/// <param name="Verification">Ce que la vérification a établi.</param>
public sealed record ExtractResult(
    SpecNode Document,
    ReadOnlyMemory<byte> Serialized,
    DependencyClosure Closure,
    IReadOnlyList<string> CarriedTags,
    IReadOnlyList<InheritanceRecord> InheritedContext,
    IReadOnlyList<Finding> DanglingExtensionReferences,
    ExtractProvenance Provenance,
    ExtractVerification Verification)
{
    /// <summary>Indique si l'extrait peut être publié.</summary>
    public bool Publishable => Verification.Established && Serialized.Length > 0;
}
