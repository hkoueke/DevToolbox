namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>
/// Un nœud de la description, tel qu'il a été écrit.
/// </summary>
/// <remarks>
/// Cet arbre est la seule représentation que l'outil publie ou compare. Il porte la position, l'étendue en
/// octets et, pour un scalaire, la manière dont il était écrit — trois faits qu'un modèle sémantique perd
/// et sans lesquels la moitié de ce que cet outil promet est impossible : une clé en double localisée, un
/// audit de mise en forme, une économie mesurée en octets, et la survie intacte d'une extension que
/// l'outil ne comprend pas.
/// <para>
/// Invariant : l'arbre est fidèle au sens <em>et</em> à l'écriture. Réémettre un arbre non modifié doit
/// reproduire l'entrée octet pour octet, et c'est ce qu'un test de propriété vérifie sur tout le corpus.
/// </para>
/// </remarks>
public abstract class SpecNode
{
    /// <summary>Crée un nœud.</summary>
    /// <param name="location">Où il se trouve.</param>
    /// <param name="position">À quelle ligne et quelle colonne il commence.</param>
    /// <param name="span">Quelle étendue d'octets il occupe.</param>
    protected SpecNode(SpecPointer location, SourcePosition position, ByteSpan span)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(span);

        Location = location;
        Position = position;
        Span = span;
    }

    /// <summary>Où le nœud se trouve dans la description.</summary>
    public SpecPointer Location { get; }

    /// <summary>Où le nœud commence dans le texte.</summary>
    public SourcePosition Position { get; }

    /// <summary>Quelle étendue d'octets le nœud occupe.</summary>
    public ByteSpan Span { get; }

    /// <summary>La sorte du nœud.</summary>
    public abstract NodeKind Kind { get; }
}
