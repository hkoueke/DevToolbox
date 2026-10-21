namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Ce qu'un scalaire signifie, une fois résolu selon le schéma de base de YAML 1.2.</summary>
/// <param name="Kind">La sorte de valeur.</param>
/// <param name="Canonical">
/// La forme canonique de la valeur : ce qui sert à comparer et à empreinter. Deux écritures d'une même
/// valeur la partagent, ce qui est exactement ce qui fait qu'une remise en forme ne produit aucune
/// différence.
/// </param>
/// <param name="AmbiguityReason">
/// Pourquoi la résolution n'était pas univoque, quand elle ne l'était pas. Toujours
/// <see langword="null"/> autrement.
/// </param>
public sealed record ResolvedScalar(ScalarValueKind Kind, string Canonical, string? AmbiguityReason = null)
{
    /// <summary>Résout une chaîne, dont la résolution ne pose aucune question.</summary>
    /// <param name="text">Le texte.</param>
    /// <returns>La valeur résolue.</returns>
    public static ResolvedScalar FromString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ResolvedScalar(ScalarValueKind.Text, text);
    }

    /// <summary>Signale une écriture ambiguë.</summary>
    /// <param name="text">Le texte tel qu'écrit.</param>
    /// <param name="reason">Ce qui rend la résolution équivoque.</param>
    /// <returns>La valeur, marquée comme ambiguë.</returns>
    public static ResolvedScalar Ambiguous(string text, string reason)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new ResolvedScalar(ScalarValueKind.Ambiguous, text, reason);
    }
}
