namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Où une route composée rejoint le document de production.</summary>
/// <param name="AfterPath">
/// Le chemin existant que la nouvelle route suit, ou <see langword="null" /> pour la fin de
/// <c>paths</c>.
/// </param>
/// <remarks>
/// Exprimé comme « après quel chemin existant » et non comme un rang, pour deux raisons. Un rang serait
/// invalidé par la route composée juste avant dans la même séance ; et un développeur choisit un voisin
/// qu'il lit, non une position qu'il doit compter.
/// <para>
/// Ce choix existe parce qu'une description de production est relue par des personnes et que ses chemins
/// sont groupés à la main. L'ajout plaçait toute route nouvelle à la fin de <c>paths</c>, ce qui dispersait
/// une famille de routes qui se tiennent ensemble.
/// </para>
/// </remarks>
public sealed record InsertionPoint(string? AfterPath)
{
    /// <summary>À la fin de <c>paths</c>, qui reste le comportement par défaut.</summary>
    public static InsertionPoint AtEnd { get; } = new((string?)null);

    /// <summary>Après un chemin nommé.</summary>
    /// <param name="path">Le chemin existant que la nouvelle route suit.</param>
    /// <returns>Le point d'insertion.</returns>
    public static InsertionPoint After(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return new InsertionPoint(path);
    }

    /// <summary>Indique que la route va à la fin.</summary>
    public bool IsAtEnd => AfterPath is null;

    /// <summary>Ce que le point d'insertion dit, en une ligne.</summary>
    public string Display => AfterPath is { } after ? "après " + after : "à la fin";
}
