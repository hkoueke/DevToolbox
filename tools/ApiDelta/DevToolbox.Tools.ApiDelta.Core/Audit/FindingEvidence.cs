namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Sur quoi un constat repose.</summary>
/// <param name="Occurrences">Le nombre d'occurrences comptées, quand le constat porte sur une répétition.</param>
/// <param name="Fingerprint">L'empreinte partagée, quand l'égalité a été établie par empreinte.</param>
/// <param name="DifferingFields">Les champs par lesquels les membres diffèrent, quand ils diffèrent.</param>
/// <param name="Profile">Sous quel profil l'équivalence a été établie.</param>
/// <param name="Reasoning">Le raisonnement, quand la conclusion est une suggestion et non un fait.</param>
/// <param name="OccurrenceBytes">
/// Ce qu'occupe une occurrence, telle que la règle l'a mesurée.
/// </param>
/// <param name="ReferenceBytes">
/// Ce qu'occuperait la référence qui remplacerait une occurrence.
/// </param>
/// <remarks>
/// Une règle ne doit pas énoncer comme un fait ce qu'elle n'a pas établi. Ce qui est compté est compté,
/// ce qui est déduit porte son raisonnement, et le lecteur voit lequel des deux il a devant lui.
/// <para>
/// Les deux tailles sont là pour que l'économie annoncée soit vérifiable : un lecteur retrouve le chiffre
/// à partir d'elles. C'est aussi ce qui permet au chiffrage de reposer sur la mesure de la règle plutôt
/// que de la refaire — seule la règle sait ce qu'elle a mesuré, car un en-tête répété se mesure sur
/// l'en-tête et non sur l'opération qui le porte.
/// </para>
/// </remarks>
public sealed record FindingEvidence(
    int? Occurrences = null,
    string? Fingerprint = null,
    IReadOnlyList<string>? DifferingFields = null,
    string? Profile = null,
    string? Reasoning = null,
    long? OccurrenceBytes = null,
    int? ReferenceBytes = null)
{
    /// <summary>Les champs par lesquels les membres diffèrent, jamais nuls.</summary>
    public IReadOnlyList<string> DifferingFields { get; init; } = DifferingFields ?? [];
}
