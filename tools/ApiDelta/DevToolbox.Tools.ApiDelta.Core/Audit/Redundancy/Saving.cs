using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Optimization;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Ce qu'une extraction ferait gagner, tel qu'un constat l'annonce.
/// </summary>
/// <remarks>
/// Une façade, et rien de plus : l'arithmétique vit dans <see cref="SavingsEstimate"/>, la mesure dans
/// <see cref="SizeMeasurement"/>. Deux calculs du même chiffre finiraient par donner deux chiffres, et le
/// rapport se contredirait d'une section à l'autre — la section des constats annonçant une économie que
/// la section du chiffrage démentirait.
/// </remarks>
internal static class Saving
{
    /// <summary>La taille de la référence qui remplacerait une occurrence.</summary>
    /// <param name="family">La famille de composants visée.</param>
    /// <param name="name">Le nom du composant.</param>
    /// <returns>Le nombre d'octets.</returns>
    internal static int ReferenceBytes(string family, string name) =>
        SizeMeasurement.ReferenceBytes(family, name);

    /// <summary>Les octets gagnés par l'extraction d'un groupe.</summary>
    /// <param name="occurrenceBytes">La taille d'une occurrence.</param>
    /// <param name="occurrences">Leur nombre.</param>
    /// <param name="referenceBytes">La taille de la référence qui les remplacerait.</param>
    /// <returns>Les octets gagnés, ou <see langword="null"/> si l'extraction ne gagne rien.</returns>
    internal static long? Bytes(long occurrenceBytes, int occurrences, int referenceBytes) =>
        SavingsEstimate.Gain(occurrenceBytes, occurrences, referenceBytes);

    /// <summary>Le nombre d'occurrences à partir duquel l'extraction commence à gagner.</summary>
    /// <param name="occurrenceBytes">La taille d'une occurrence.</param>
    /// <param name="referenceBytes">La taille de la référence.</param>
    /// <returns>Le seuil, ou <see langword="null"/> si l'extraction ne gagne jamais.</returns>
    internal static int? BreakEven(long occurrenceBytes, int referenceBytes) =>
        SavingsEstimate.BreakEven(occurrenceBytes, referenceBytes);
}
