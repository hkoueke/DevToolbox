using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>Une opération telle que l'écran de sélection la montre.</summary>
/// <param name="Selector">Ce que le développeur retiendra.</param>
/// <param name="Summary">Le résumé, quand la description en donne un.</param>
/// <param name="Tags">Les étiquettes, qui servent à filtrer et à grouper.</param>
/// <param name="Deprecated">L'opération est déclarée obsolète.</param>
/// <param name="Location">Où elle se trouve dans l'arbre.</param>
/// <remarks>
/// Aucun schéma, aucun exemple, aucun nœud de contenu : lister quatre cents opérations doit coûter quatre
/// cents chaînes courtes. Un catalogue qui porterait les sous-arbres rendrait le simple fait d'ouvrir
/// l'écran de sélection aussi cher que l'examen complet.
/// </remarks>
public sealed record SelectableOperation(
    OperationSelector Selector,
    string? Summary,
    IReadOnlyList<string> Tags,
    bool Deprecated,
    SpecPointer Location)
{
    /// <summary>Les étiquettes, jamais nulles.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Tags ?? [];

    /// <summary>La première étiquette, ou le groupe des opérations qui n'en portent aucune.</summary>
    public string Group => Tags.Count > 0 ? Tags[0] : "Sans étiquette";

    /// <summary>La ligne affichée : la méthode, le chemin, et le résumé s'il y en a un.</summary>
    public string Display => string.IsNullOrWhiteSpace(Summary)
        ? Selector.Display
        : Selector.Display + " — " + Summary;
}
