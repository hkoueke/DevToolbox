using DevToolbox.Tools.VarCompare.Core.Comparison;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Le symbole, le mot et la couleur associés à chaque état de cellule.
/// </summary>
/// <remarks>
/// La couleur n'est qu'un renfort. Le symbole et le mot portent le sens, de sorte que chaque état reste
/// identifiable même sans couleur. L'Unicode se replie sur l'ASCII quand le terminal ne sait pas l'afficher.
/// </remarks>
public sealed class SymbolSet
{
    private readonly bool _unicode;

    /// <summary>Crée un jeu de symboles adapté à un terminal.</summary>
    /// <param name="unicode">Si le terminal restitue l'Unicode.</param>
    public SymbolSet(bool unicode) => _unicode = unicode;

    /// <summary>La marque d'une ligne dont les valeurs lisibles diffèrent.</summary>
    public string DiffersMarker => _unicode ? "≠" : "!=";

    /// <summary>La marque d'une ligne dont les groupes orthographient le nom différemment.</summary>
    public static string CasingMarker => "~";

    /// <summary>La marque d'une cellule provenant d'un coffre de clés.</summary>
    public static string KeyVaultQualifier => "[kv]";

    /// <summary>La marque d'une cellule en lecture seule.</summary>
    public static string ReadOnlyQualifier => "[ro]";

    /// <summary>Le symbole associé à un état.</summary>
    /// <param name="state">L'état de la cellule.</param>
    /// <returns>Un symbole que le terminal sait afficher.</returns>
    public string Symbol(CellState state) => state switch
    {
        CellState.PresentWithValue => _unicode ? "✔" : "+",
        CellState.PresentEmpty => _unicode ? "○" : "o",
        CellState.PresentSecret => _unicode ? "🔒" : "#",
        CellState.Absent => _unicode ? "✘" : "x",
        _ => "?",
    };

    /// <summary>Le mot associé à un état. C'est lui qui rend l'affichage lisible sans couleur.</summary>
    /// <param name="state">L'état de la cellule.</param>
    /// <returns>Un mot court.</returns>
    public static string Text(CellState state) => state switch
    {
        CellState.PresentWithValue => "set",
        CellState.PresentEmpty => "empty",
        CellState.PresentSecret => "secret",
        CellState.Absent => "missing",
        _ => "unknown",
    };

    /// <summary>La couleur Spectre associée à un état.</summary>
    /// <param name="state">L'état de la cellule.</param>
    /// <returns>Un nom de couleur de balisage.</returns>
    public static string Colour(CellState state) => state switch
    {
        CellState.PresentWithValue => "green",
        CellState.PresentEmpty => "yellow",
        CellState.PresentSecret => "blue",
        CellState.Absent => "red",
        _ => "grey",
    };
}
