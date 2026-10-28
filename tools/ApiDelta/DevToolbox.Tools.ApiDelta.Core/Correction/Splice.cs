using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Un changement ponctuel à apporter aux octets de la production.</summary>
/// <param name="Kind">Ce qu'il faut faire à cet endroit.</param>
/// <param name="Target">
/// L'emplacement visé : la valeur à remplacer, l'association à laquelle ajouter, ou l'entrée à retirer.
/// </param>
/// <param name="Key">La clé de l'entrée à ajouter, pour <see cref="SpliceKind.Add"/> seulement.</param>
/// <param name="Value">Ce qu'il faut écrire, pour un remplacement ou un ajout.</param>
/// <param name="AfterKey">
/// L'entrée voisine après laquelle ajouter, pour <see cref="SpliceKind.Add"/> seulement.
/// <see langword="null"/> ajoute à la fin de l'association, ce qui était le seul comportement possible.
/// </param>
/// <remarks>
/// Une épissure est dite en termes d'emplacement et de contenu, jamais en termes de décalage d'octets :
/// c'est l'épisseur qui traduit l'emplacement en étendue, parce que lui seul connaît l'arbre de la
/// production. Des décalages calculés par l'appelant se décaleraient les uns les autres dès la deuxième
/// épissure.
/// <para>
/// <paramref name="AfterKey"/> est dit par le nom du voisin et non par un rang, pour la même raison : un
/// rang serait invalidé par l'épissure précédente. Il existe parce qu'une description de production est
/// relue par des personnes et que ses chemins sont groupés à la main — ajouter toute route nouvelle à la
/// fin dispersait une famille de routes qui se tiennent ensemble.
/// </para>
/// </remarks>
public sealed record Splice(
    SpliceKind Kind,
    SpecPointer Target,
    string? Key = null,
    SpecNode? Value = null,
    string? AfterKey = null);
