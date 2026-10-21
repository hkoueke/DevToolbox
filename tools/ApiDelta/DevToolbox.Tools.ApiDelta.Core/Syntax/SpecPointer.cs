using System.Text;

namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>
/// Où une chose se trouve dans une description : une fois pour une machine, une fois pour un lecteur.
/// </summary>
/// <remarks>
/// Les deux formes vivent ensemble parce que les rapports ont besoin des deux et que la forme lisible ne
/// peut pas être reconstituée au moment de l'affichage : seule la traversée sait qu'un paramètre
/// s'appelle <c>X-serviceEmetteur</c> et appartient à <c>POST /documents</c>.
/// </remarks>
public sealed class SpecPointer : IEquatable<SpecPointer>
{
    private readonly string[] _segments;

    private string? _json;

    private SpecPointer(string[] segments, string display)
    {
        _segments = segments;
        Display = display;
    }

    /// <summary>La racine du document.</summary>
    public static SpecPointer Root { get; } = new([], "le document");

    /// <summary>Les segments traversés, de la racine jusqu'ici.</summary>
    /// <remarks>
    /// Exposés pour le métamodèle, qui descend l'arbre en suivant les sortes d'objets : il a besoin du
    /// chemin segment par segment, et non de sa forme imprimée.
    /// </remarks>
    public IReadOnlyList<string> Segments => _segments;

    /// <summary>La forme destinée aux machines, selon la RFC 6901.</summary>
    /// <remarks>
    /// Construite une fois puis retenue. Le pointeur est immuable, si bien que la mémoriser ne peut pas
    /// mentir — et elle est demandée partout : la traversée range ses nœuds par pointeur, chaque
    /// comparaison de tri la réclamait, et sur une description de dix mégaoctets cela faisait des
    /// millions de chaînes reconstruites pour le même résultat.
    /// </remarks>
    public string Json => _json ??= BuildJson(_segments);

    /// <summary>La forme destinée à un lecteur.</summary>
    public string Display { get; }

    /// <summary>Descend d'un cran, vers une clé.</summary>
    /// <param name="key">La clé traversée.</param>
    /// <param name="display">Ce qu'il faut afficher à la place de la clé, le cas échéant.</param>
    /// <returns>Le pointeur de l'enfant.</returns>
    public SpecPointer Child(string key, string? display = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        return new SpecPointer([.. _segments, key], Append(display ?? key));
    }

    /// <summary>Descend d'un cran, vers un rang de séquence.</summary>
    /// <param name="index">Le rang, à partir de zéro.</param>
    /// <param name="display">Ce qu'il faut afficher à la place du rang, le cas échéant.</param>
    /// <returns>Le pointeur de l'enfant.</returns>
    public SpecPointer Item(int index, string? display = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        string text = index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new SpecPointer([.. _segments, text], Append(display ?? "[" + text + "]"));
    }

    /// <summary>
    /// Indique si ce pointeur désigne l'autre ou un de ses ancêtres.
    /// </summary>
    /// <param name="other">Le pointeur à situer.</param>
    /// <returns><see langword="true"/> si l'autre est ici ou dessous.</returns>
    /// <remarks>
    /// La racine contient tout, y compris elle-même. C'est ce qui permet de refuser une édition dont
    /// l'origine engloberait sa propre destination : remplacer l'origine emporterait alors ce qu'on vient
    /// d'y poser.
    /// </remarks>
    public bool Contains(SpecPointer other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return other._segments.Length >= _segments.Length
            && other._segments.AsSpan(0, _segments.Length).SequenceEqual(_segments);
    }

    /// <inheritdoc />
    public bool Equals(SpecPointer? other) =>
        other is not null && _segments.AsSpan().SequenceEqual(other._segments);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SpecPointer);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;

        foreach (string segment in _segments)
        {
            hash.Add(segment, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => Json;

    /// <summary>
    /// Assemble la forme RFC 6901. Les caractères <c>~</c> et <c>/</c> y sont échappés, sans quoi un chemin
    /// tel que <c>/documents</c> couperait le pointeur en deux.
    /// </summary>
    private static string BuildJson(string[] segments)
    {
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();

        foreach (string segment in segments)
        {
            builder.Append('/')
                .Append(segment.Replace("~", "~0", StringComparison.Ordinal)
                    .Replace("/", "~1", StringComparison.Ordinal));
        }

        return builder.ToString();
    }

    private string Append(string display) =>
        _segments.Length == 0 ? display : Display + " → " + display;
}
