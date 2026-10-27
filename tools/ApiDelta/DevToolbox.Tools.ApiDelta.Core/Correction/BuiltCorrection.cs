namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>La production consolidée, telle qu'elle sera écrite.</summary>
/// <param name="Serialized">Les octets exacts qui seront publiés.</param>
/// <param name="Plan">Ce qui y a été inséré, pour que la vérification ait de quoi se mesurer.</param>
/// <param name="Marks">Les traces posées, une par élément ajouté ou modifié.</param>
/// <remarks>
/// Les octets, et non un arbre : la vérification doit porter sur ce qui sera écrit. Vérifier l'arbre
/// modifié laisserait passer exactement les défauts que l'écriture introduit — c'est la leçon que
/// l'optimiseur de cet outil avait déjà apprise.
/// </remarks>
public sealed record BuiltCorrection(
    ReadOnlyMemory<byte> Serialized,
    CorrectionPlan Plan,
    IReadOnlyList<CorrectionMark> Marks);
