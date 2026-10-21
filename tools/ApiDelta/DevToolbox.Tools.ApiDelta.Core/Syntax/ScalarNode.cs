namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Une valeur terminale, telle qu'elle était écrite et telle qu'elle se résout.</summary>
/// <remarks>
/// Les deux formes coexistent délibérément. <see cref="Text"/> est ce que l'auteur a tapé, et c'est lui
/// qui permet de signaler qu'un code de réponse est quoté ici et pas là. <see cref="Resolved"/> est ce que
/// la valeur signifie, et c'est lui qui permet de ne pas voir deux codes là où il n'y en a qu'un.
/// <para>
/// Quand la résolution n'est pas univoque — <c>y</c>, <c>no</c>, <c>on</c>, <c>~</c>, un nombre à zéro
/// initial, une date non quotée — la valeur est signalée comme ambiguë plutôt que tranchée en silence.
/// Choisir pour l'auteur, c'est décider à sa place ce que dit son contrat.
/// </para>
/// </remarks>
public sealed class ScalarNode : SpecNode
{
    /// <summary>Crée un scalaire.</summary>
    /// <param name="location">Où il se trouve.</param>
    /// <param name="position">Où il commence.</param>
    /// <param name="span">Quelle étendue il occupe.</param>
    /// <param name="text">Le texte tel qu'il était écrit, guillemets retirés.</param>
    /// <param name="style">La manière dont il était écrit.</param>
    /// <param name="resolved">Ce que la valeur signifie.</param>
    /// <param name="tag">L'étiquette explicite, s'il y en avait une.</param>
    public ScalarNode(
        SpecPointer location,
        SourcePosition position,
        ByteSpan span,
        string text,
        ScalarStyle style,
        ResolvedScalar resolved,
        string? tag = null)
        : base(location, position, span)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(resolved);

        Text = text;
        Style = style;
        Resolved = resolved;
        Tag = tag;
    }

    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Scalar;

    /// <summary>Le texte tel qu'il était écrit.</summary>
    public string Text { get; }

    /// <summary>La manière dont il était écrit.</summary>
    public ScalarStyle Style { get; }

    /// <summary>Ce que la valeur signifie.</summary>
    public ResolvedScalar Resolved { get; }

    /// <summary>L'étiquette explicite, s'il y en avait une.</summary>
    public string? Tag { get; }
}
