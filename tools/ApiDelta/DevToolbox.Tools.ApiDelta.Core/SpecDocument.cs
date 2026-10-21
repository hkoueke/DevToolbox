using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Une description lue, dans le rôle qu'elle tient.</summary>
/// <param name="Role">Production ou candidate.</param>
/// <param name="Path">Son chemin canonique.</param>
/// <param name="Format">La forme sous laquelle elle était écrite, détectée d'après le contenu.</param>
/// <param name="DeclaredVersion">La version d'OpenAPI qu'elle déclare.</param>
/// <param name="ByteLength">Sa taille.</param>
/// <param name="ContentHash">Son empreinte, en hexadécimal minuscule.</param>
/// <param name="Root">L'arbre de syntaxe, fidèle au sens et à l'écriture.</param>
/// <param name="Source">
/// Les octets d'origine. Conservés parce que l'émission fidèle réassemble le document à partir des
/// étendues de l'arbre : c'est ce qui rend vérifiable la promesse qu'un arbre non modifié se réécrit à
/// l'identique, et c'est aussi ce qui rendra atteignable, plus tard, une réécriture qui préserve les
/// commentaires.
/// </param>
/// <remarks>
/// Invariant : une instance de ce type n'existe que si la description a été lue fidèlement. Une erreur de
/// syntaxe, une clé en double, une référence non résolue ou une version non prise en charge produisent des
/// diagnostics et aucun document — si bien qu'aucune étape ultérieure ne peut recevoir une description à
/// moitié lue.
/// </remarks>
public sealed record SpecDocument(
    DocumentRole Role,
    string Path,
    SpecFormat Format,
    OpenApiVersion DeclaredVersion,
    long ByteLength,
    string ContentHash,
    SpecNode Root,
    ReadOnlyMemory<byte> Source);
