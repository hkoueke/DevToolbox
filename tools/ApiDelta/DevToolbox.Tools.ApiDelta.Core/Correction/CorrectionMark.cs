using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>La trace qu'un élément ajouté ou modifié porte dans le document.</summary>
/// <param name="Target">Où la trace est posée.</param>
/// <param name="From">D'où le contenu vient, dans le candidat.</param>
/// <param name="Fields">Les champs concernés, quand la modification n'a pas porté sur l'objet entier.</param>
/// <param name="Replaced">Ce que la production disait, par champ, sous forme bornée.</param>
/// <param name="Derived">
/// Le contrat de service d'où l'élément a été dérivé, quand il l'a été plutôt que recopié.
/// </param>
/// <remarks>
/// Elle porte ce qui a été <em>remplacé</em>, et non seulement ce qui a été écrit. Une modification qui ne
/// consignerait que son résultat laisserait un relecteur incapable de voir ce que la correction a coûté :
/// il verrait la nouvelle valeur sans savoir qu'il y en avait une autre.
/// <para>
/// Posée sur l'objet le plus proche capable de porter une extension. Un champ scalaire — <c>required</c>,
/// <c>type</c> — ne peut pas en porter une lui-même : OpenAPI n'autorise les membres <c>x-</c> que sur
/// des objets. La trace nomme donc le champ au lieu de s'y accrocher.
/// </para>
/// <para>
/// L'origine dérivée est le seul endroit où une correction issue d'un contrat de service diffère d'une
/// correction issue d'une description OpenAPI. Là, le contenu vient des octets du candidat ; ici, il
/// vient d'une traduction, et un élément dérivé qui serait indiscernable d'un élément recopié ferait
/// prendre une décision de mise en production sur une description que personne n'a écrite.
/// </para>
/// </remarks>
public sealed record CorrectionMark(
    SpecPointer Target,
    string From,
    IReadOnlyList<string>? Fields = null,
    IReadOnlyDictionary<string, string>? Replaced = null,
    DerivedOrigin? Derived = null)
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

    /// <summary>Indique que l'élément a été dérivé d'un contrat de service, et non recopié.</summary>
    public bool IsDerived => Derived is not null;
}
