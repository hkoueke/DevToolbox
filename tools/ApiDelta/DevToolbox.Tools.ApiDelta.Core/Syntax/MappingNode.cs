namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Une association de clés à des nœuds.</summary>
/// <remarks>
/// Les entrées restent dans l'ordre où elles étaient écrites. L'ordre d'une association n'a pas de sens
/// pour OpenAPI, mais il en a un pour le document : c'est ce qui permet de recommander un ordre stable
/// sans inventer la différence que l'ordre actuel constituerait.
/// <para>
/// Les clés en double sont possibles ici, et c'est voulu : les détecter demande de pouvoir les porter. Le
/// lecteur les signale et refuse la description, mais l'arbre ne mentira pas sur ce qui était écrit.
/// </para>
/// </remarks>
public sealed class MappingNode : SpecNode
{
    /// <summary>Crée une association.</summary>
    /// <param name="location">Où elle se trouve.</param>
    /// <param name="position">Où elle commence.</param>
    /// <param name="span">Quelle étendue elle occupe.</param>
    /// <param name="entries">Ses entrées, dans l'ordre d'écriture.</param>
    public MappingNode(
        SpecPointer location,
        SourcePosition position,
        ByteSpan span,
        IReadOnlyList<MappingEntry> entries)
        : base(location, position, span)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
    }

    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Mapping;

    /// <summary>Les entrées, dans l'ordre d'écriture.</summary>
    public IReadOnlyList<MappingEntry> Entries { get; }

    /// <summary>Cherche la valeur associée à une clé.</summary>
    /// <param name="key">La clé cherchée.</param>
    /// <returns>La valeur, ou <see langword="null"/> si la clé est absente.</returns>
    /// <remarks>
    /// Rend la première occurrence. Une description comportant une clé en double n'arrive jamais jusqu'ici,
    /// puisque le lecteur la refuse.
    /// </remarks>
    public SpecNode? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Entries
            .FirstOrDefault(entry => string.Equals(entry.Key.Text, key, StringComparison.Ordinal))
            ?.Value;
    }
}
