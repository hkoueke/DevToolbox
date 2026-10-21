using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Lit une description en un arbre fidèle, ou explique pourquoi elle ne l'est pas.</summary>
/// <remarks>
/// La forme est détectée d'après le contenu, les clés en double sont relevées <em>avant</em> que quoi que
/// ce soit ne les lie, et chaque limite atteinte est nommée. Un échec est une liste de diagnostics situés,
/// jamais une exception.
/// </remarks>
public interface ISpecReader
{
    /// <summary>Lit une description.</summary>
    /// <param name="path">Le chemin du fichier.</param>
    /// <param name="role">Le rôle que la description tient dans l'exécution.</param>
    /// <param name="limits">Le budget que le document ne peut pas dépasser.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Le document, ou de quoi expliquer son absence.</returns>
    Task<SpecReadResult> ReadAsync(
        string path,
        DocumentRole role,
        LoadLimits limits,
        CancellationToken cancellationToken);

    /// <summary>Lit une description déjà en mémoire.</summary>
    /// <param name="content">Les octets.</param>
    /// <param name="name">Le nom à donner à la description dans les diagnostics.</param>
    /// <param name="role">Le rôle qu'elle tient.</param>
    /// <param name="limits">Le budget qu'elle ne peut pas dépasser.</param>
    /// <returns>Le document, ou de quoi expliquer son absence.</returns>
    /// <remarks>
    /// Sans entrée-sortie, donc sans attente. Son emploi n'est pas une commodité : la vérification d'une
    /// copie optimisée doit relire <em>les octets qui seront écrits</em>, par le même lecteur que le
    /// fichier d'origine. Vérifier l'arbre modifié plutôt que sa sérialisation laisserait passer
    /// exactement les défauts que la sérialisation introduit.
    /// </remarks>
    SpecReadResult ReadBytes(
        ReadOnlyMemory<byte> content,
        string name,
        DocumentRole role,
        LoadLimits limits);
}
