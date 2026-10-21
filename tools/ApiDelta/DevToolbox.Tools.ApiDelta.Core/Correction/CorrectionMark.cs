using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>La trace qu'un élément ajouté ou modifié porte dans le document.</summary>
/// <param name="Target">Où la trace est posée.</param>
/// <param name="From">D'où le contenu vient, dans le candidat.</param>
/// <param name="Fields">Les champs concernés, quand la modification n'a pas porté sur l'objet entier.</param>
/// <param name="Replaced">Ce que la production disait, par champ, sous forme bornée.</param>
/// <remarks>
/// Elle porte ce qui a été <em>remplacé</em>, et non seulement ce qui a été écrit. Une modification qui ne
/// consignerait que son résultat laisserait un relecteur incapable de voir ce que la correction a coûté :
/// il verrait la nouvelle valeur sans savoir qu'il y en avait une autre.
/// <para>
/// Posée sur l'objet le plus proche capable de porter une extension. Un champ scalaire — <c>required</c>,
/// <c>type</c> — ne peut pas en porter une lui-même : OpenAPI n'autorise les membres <c>x-</c> que sur
/// des objets. La trace nomme donc le champ au lieu de s'y accrocher.
/// </para>
/// </remarks>
public sealed record CorrectionMark(
    SpecPointer Target,
    string From,
    IReadOnlyList<string>? Fields = null,
    IReadOnlyDictionary<string, string>? Replaced = null)
{
    /// <summary>Le nom du membre d'extension qui porte la trace.</summary>
    public const string Member = "x-apidelta-origin";

    /// <summary>Les champs concernés, jamais nuls.</summary>
    public IReadOnlyList<string> Fields { get; init; } = Fields ?? [];

    /// <summary>Ce qui a été remplacé, jamais nul.</summary>
    public IReadOnlyDictionary<string, string> Replaced { get; init; } =
        Replaced ?? new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Indique que la trace porte sur un élément entièrement neuf.</summary>
    public bool IsAddition => Fields.Count == 0 && Replaced.Count == 0;
}
