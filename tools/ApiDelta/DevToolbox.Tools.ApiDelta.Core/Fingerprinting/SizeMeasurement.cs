using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>
/// Mesure ce qu'une chose occupe réellement dans le fichier.
/// </summary>
/// <remarks>
/// Mesuré, et non estimé à vue : l'arbre porte l'étendue en octets de chaque nœud, si bien que la taille
/// d'une occurrence est un fait et non une approximation. C'est l'une des raisons pour lesquelles cet
/// outil lit la syntaxe plutôt qu'un modèle sémantique — un modèle sémantique aurait perdu cette étendue,
/// et une économie annoncée sans mesure n'est qu'une opinion.
/// </remarks>
public static class SizeMeasurement
{
    /// <summary>Ce qu'un nœud occupe.</summary>
    /// <param name="node">Le nœud.</param>
    /// <returns>Le nombre d'octets.</returns>
    public static long Of(SpecNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Span.Length;
    }

    /// <summary>Ce que plusieurs nœuds occupent ensemble.</summary>
    /// <param name="nodes">Les nœuds.</param>
    /// <returns>Le nombre d'octets.</returns>
    public static long Total(IEnumerable<SpecNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        return nodes.Sum(Of);
    }

    /// <summary>Ce qu'occupe le plus grand de plusieurs nœuds.</summary>
    /// <param name="nodes">Les nœuds.</param>
    /// <returns>Le nombre d'octets, ou zéro s'il n'y a aucun nœud.</returns>
    /// <remarks>
    /// Le plus grand, parce que c'est lui qui subsisterait : extraire un groupe garde une écriture et
    /// remplace les autres par des références. Prendre la moyenne annoncerait une économie que la plus
    /// grande des occurrences démentirait.
    /// </remarks>
    public static long Largest(IEnumerable<SpecNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        return nodes.Select(Of).DefaultIfEmpty(0).Max();
    }

    /// <summary>Ce qu'occuperait la référence qui remplacerait une occurrence.</summary>
    /// <param name="family">La famille de composants visée.</param>
    /// <param name="name">Le nom du composant.</param>
    /// <returns>Le nombre d'octets.</returns>
    /// <remarks>
    /// Une référence n'est pas gratuite, et c'est ce qui fait qu'une extraction à deux occurrences ne
    /// gagne presque rien. Le dire évite de recommander un dérangement pour rien.
    /// </remarks>
    public static int ReferenceBytes(string family, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentNullException.ThrowIfNull(name);

        return "$ref: '#/components//'".Length + family.Length + name.Length;
    }
}
