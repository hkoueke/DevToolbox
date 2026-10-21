using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Ce qu'une extraction ferait gagner, et à partir de combien d'occurrences elle commence à gagner.
/// </summary>
/// <param name="CurrentBytes">Ce que les occurrences occupent aujourd'hui.</param>
/// <param name="ProjectedBytes">Ce qu'occuperaient le composant partagé et ses références.</param>
/// <param name="BreakEvenOccurrences">
/// Le nombre d'occurrences à partir duquel l'extraction gagne, ou <see langword="null"/> quand elle ne
/// gagne jamais.
/// </param>
/// <param name="Safety">Ce que l'outil peut prouver au sujet du remède.</param>
/// <remarks>
/// Un chiffrage qui ne pourrait pas annoncer une perte serait une publicité. Celui-ci peut : l'économie
/// absolue est une soustraction, elle vaut ce qu'elle vaut, et <see cref="Recommended"/> est faux quand
/// elle est nulle ou négative. Un groupe en dessous de son seuil de rentabilité est donc rapporté — parce
/// qu'il est là — mais n'est pas recommandé, et la différence est portée par le type et non par l'usage
/// qu'on en fait.
/// </remarks>
public sealed record SavingsEstimate(
    long CurrentBytes,
    long ProjectedBytes,
    int? BreakEvenOccurrences,
    SafetyClass Safety)
{
    /// <summary>Un chiffrage sans objet.</summary>
    public static SavingsEstimate None { get; } = new(0, 0, null, SafetyClass.Unsafe);

    /// <summary>Les octets gagnés, négatifs quand l'extraction coûterait plus qu'elle ne rapporte.</summary>
    public long AbsoluteSaving => CurrentBytes - ProjectedBytes;

    /// <summary>La part gagnée, en pourcentage de ce que les occurrences occupent.</summary>
    public double PercentSaving =>
        CurrentBytes <= 0 ? 0 : Math.Round(AbsoluteSaving * 100d / CurrentBytes, 1);

    /// <summary>
    /// Indique si l'extraction vaut la peine d'être proposée.
    /// </summary>
    /// <remarks>
    /// Deux conditions, et aucune n'est négociable : il faut qu'elle gagne quelque chose, et il faut que
    /// l'outil ait quelque chose à dire de sa sûreté. Une transformation qui n'est que signalée n'est
    /// jamais une proposition, même quand elle gagnerait beaucoup.
    /// </remarks>
    public bool Recommended => AbsoluteSaving > 0 && Safety != SafetyClass.Unsafe;

    /// <summary>Chiffre l'extraction d'un groupe d'occurrences de même taille.</summary>
    /// <param name="occurrenceBytes">Ce qu'occupe une occurrence, prise comme la plus grande.</param>
    /// <param name="occurrences">Leur nombre.</param>
    /// <param name="referenceBytes">Ce qu'occuperait la référence qui en remplacerait une.</param>
    /// <param name="safety">Ce que l'outil peut prouver au sujet du remède.</param>
    /// <returns>Le chiffrage.</returns>
    public static SavingsEstimate For(
        long occurrenceBytes,
        int occurrences,
        int referenceBytes,
        SafetyClass safety)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(occurrenceBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(occurrences);
        ArgumentOutOfRangeException.ThrowIfNegative(referenceBytes);

        // Après extraction : une déclaration partagée, et une référence par occurrence.
        return new SavingsEstimate(
            occurrenceBytes * occurrences,
            occurrenceBytes + ((long)referenceBytes * occurrences),
            BreakEven(occurrenceBytes, referenceBytes),
            safety);
    }

    /// <summary>Les octets gagnés, ou rien quand l'extraction ne gagne pas.</summary>
    /// <param name="occurrenceBytes">Ce qu'occupe une occurrence.</param>
    /// <param name="occurrences">Leur nombre.</param>
    /// <param name="referenceBytes">Ce qu'occuperait la référence.</param>
    /// <returns>Les octets gagnés, ou <see langword="null"/>.</returns>
    /// <remarks>
    /// La forme qu'emploie un constat, qui n'affiche un chiffre que lorsqu'il y a un chiffre à afficher.
    /// « Économie estimée : 0 octet » se lit comme un oubli, alors que l'absence se lit comme ce qu'elle
    /// est.
    /// </remarks>
    public static long? Gain(long occurrenceBytes, int occurrences, int referenceBytes)
    {
        SavingsEstimate estimate = For(
            occurrenceBytes, occurrences, referenceBytes, SafetyClass.Safe);

        return estimate.AbsoluteSaving > 0 ? estimate.AbsoluteSaving : null;
    }

    /// <summary>Le nombre d'occurrences à partir duquel l'extraction commence à gagner.</summary>
    /// <param name="occurrenceBytes">Ce qu'occupe une occurrence.</param>
    /// <param name="referenceBytes">Ce qu'occuperait la référence.</param>
    /// <returns>Le seuil, ou <see langword="null"/> si l'extraction ne gagne jamais.</returns>
    /// <remarks>
    /// Une occurrence plus courte que la référence qui la remplacerait ne gagne à aucun compte : le seuil
    /// n'existe alors pas, et prétendre le contraire reviendrait à recommander d'allonger le fichier.
    /// </remarks>
    public static int? BreakEven(long occurrenceBytes, int referenceBytes)
    {
        if (occurrenceBytes <= referenceBytes)
        {
            return null;
        }

        // n × taille > taille + n × référence  ⇔  n > taille / (taille − référence)
        return (int)(occurrenceBytes / (occurrenceBytes - referenceBytes)) + 1;
    }
}
