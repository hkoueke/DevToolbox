using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Écrit un arbre sous une forme choisie.</summary>
/// <remarks>
/// Distinct de l'émetteur fidèle : celui-ci produit une écriture neuve, avec l'indentation, la quotation
/// et l'ordre des clés demandés. C'est ce qu'il faut pour une copie remise en forme, un extrait, ou les
/// seules parties neuves d'une production consolidée — et c'est aussi pourquoi aucun commentaire n'y survit.
/// </remarks>
public interface ISpecEmitter
{
    /// <summary>Écrit un document entier.</summary>
    /// <param name="root">La racine à écrire.</param>
    /// <param name="formatting">La mise en forme demandée.</param>
    /// <returns>Les octets écrits.</returns>
    ReadOnlyMemory<byte> Emit(SpecNode root, CanonicalFormatting formatting);
}
