using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>
/// Réduit un nœud à ce que le document dit, en écartant la manière dont il est écrit.
/// </summary>
/// <remarks>
/// La comparaison compare tout : chaque champ, chaque extension, chaque valeur. Une différence de
/// documentation est une différence, même si elle ne casse rien, et c'est le classement qui en décide
/// ensuite, non la canonisation.
/// <para>
/// Une seule canonisation existe dans l'outil, et ce raccourci la désigne sous son profil le plus
/// exigeant. Deux implémentations qui doivent s'accorder sont une panne qu'aucun compilateur n'annonce.
/// </para>
/// </remarks>
internal static class CanonicalForm
{
    internal static string Of(
        SpecNode node,
        IMetamodel metamodel,
        OpenApiVersion version) =>
        Canonicalizer.Write(node, metamodel, version, EquivalenceProfile.Strict);
}
