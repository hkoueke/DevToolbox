namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Une suite ordonnée de nœuds.</summary>
/// <remarks>
/// L'ordre est conservé sans jugement. Savoir si l'ordre de telle suite a un sens pour OpenAPI — il en a
/// un pour les alternatives de sécurité, aucun pour un ensemble <c>required</c> — est l'affaire du
/// métamodèle, pas de l'arbre.
/// </remarks>
public sealed class SequenceNode : SpecNode
{
    /// <summary>Crée une suite.</summary>
    /// <param name="location">Où elle se trouve.</param>
    /// <param name="position">Où elle commence.</param>
    /// <param name="span">Quelle étendue elle occupe.</param>
    /// <param name="items">Ses éléments, dans l'ordre d'écriture.</param>
    public SequenceNode(
        SpecPointer location,
        SourcePosition position,
        ByteSpan span,
        IReadOnlyList<SpecNode> items)
        : base(location, position, span)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
    }

    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Sequence;

    /// <summary>Les éléments, dans l'ordre d'écriture.</summary>
    public IReadOnlyList<SpecNode> Items { get; }
}
