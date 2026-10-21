using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>Ce qu'une valeur était, en assez court pour être lu.</summary>
/// <param name="Text">La valeur, abrégée si besoin.</param>
/// <param name="Kind">Ce qu'elle était : un scalaire, une association, une suite.</param>
/// <param name="FullLength">Sa longueur réelle, avant abrègement.</param>
/// <param name="Truncated">Indique que le texte a été abrégé.</param>
/// <remarks>
/// Bornée volontairement, pour deux raisons qui vont dans le même sens. Un rapport porte des centaines de
/// différences, et une valeur entière y rendrait la plus importante introuvable. Surtout, un résumé est
/// une chose qui s'écrit dans un fichier : un exemple de requête entier, recopié sans limite, est la fuite
/// que l'outil s'interdit par ailleurs.
/// </remarks>
public sealed record ValueSummary(string Text, NodeKind Kind, int FullLength, bool Truncated)
{
    /// <summary>La longueur au-delà de laquelle un résumé est abrégé.</summary>
    public const int MaxLength = 120;

    /// <summary>Le marqueur d'abrègement, explicite pour qu'on ne lise pas une valeur tronquée comme entière.</summary>
    public const string Elision = " […]";

    /// <summary>Résume un nœud.</summary>
    /// <param name="node">Le nœud à résumer.</param>
    /// <returns>Le résumé.</returns>
    public static ValueSummary Of(SpecNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        string text = Describe(node);

        return text.Length <= MaxLength
            ? new ValueSummary(text, node.Kind, text.Length, Truncated: false)
            : new ValueSummary(
                text[..MaxLength] + Elision, node.Kind, text.Length, Truncated: true);
    }

    /// <inheritdoc />
    public override string ToString() => Text;

    /// <summary>
    /// Décrit un nœud : sa valeur s'il est scalaire, sa forme s'il est composite.
    /// </summary>
    /// <remarks>
    /// Un composite est décrit par sa forme et non par son contenu : « association de 14 entrées » dit au
    /// lecteur ce qu'il doit savoir pour aller voir, sans recopier le document dans le rapport.
    /// </remarks>
    private static string Describe(SpecNode node) => node switch
    {
        ScalarNode scalar => scalar.Resolved.Kind == ScalarValueKind.Null ? "(vide)" : scalar.Text,
        MappingNode mapping => $"association de {mapping.Entries.Count} entrée(s)",
        SequenceNode sequence => $"suite de {sequence.Items.Count} élément(s)",
        _ => "(indéterminé)",
    };
}
