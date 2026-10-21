namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Ce que la sortie pèserait, et ce qu'on tolère qu'elle pèse.</summary>
/// <param name="ProjectedBytes">La taille projetée de la description après transformation.</param>
/// <param name="Limit">La taille au-delà de laquelle on refuse de publier, s'il y en a une.</param>
/// <param name="Exceeded">Indique si la projection dépasse cette limite.</param>
/// <remarks>
/// Un optimiseur qui grossit son sujet s'est trompé, et il doit pouvoir le dire. Le contrôle vit dans le
/// plan plutôt que dans l'écriture, de sorte que le dépassement est connu avant qu'un fichier n'ait été
/// produit — annoncé, et non découvert en comparant deux tailles après coup.
/// </remarks>
public sealed record GrowthCheck(long ProjectedBytes, long? Limit, bool Exceeded)
{
    /// <summary>Un contrôle sans objet : rien n'est projeté.</summary>
    public static GrowthCheck None { get; } = new(0, null, false);

    /// <summary>Projette la taille de la sortie et la compare à ce qui est toléré.</summary>
    /// <param name="currentBytes">La taille actuelle de la description.</param>
    /// <param name="savedBytes">Ce que les transformations retenues feraient gagner.</param>
    /// <param name="maxGrowthPercent">La croissance tolérée, en pourcentage. Zéro n'en tolère aucune.</param>
    /// <returns>Le contrôle.</returns>
    public static GrowthCheck For(long currentBytes, long savedBytes, int maxGrowthPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maxGrowthPercent);

        long projected = Math.Max(0, currentBytes - savedBytes);
        long limit = currentBytes + (currentBytes * maxGrowthPercent / 100);

        return new GrowthCheck(projected, limit, projected > limit);
    }
}
