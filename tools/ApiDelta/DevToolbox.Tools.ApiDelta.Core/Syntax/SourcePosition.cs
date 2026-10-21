namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>Où une chose se trouve dans le texte d'origine.</summary>
/// <param name="Line">La ligne, à partir de un.</param>
/// <param name="Column">La colonne, à partir de un.</param>
/// <remarks>
/// Numérotées à partir de un parce qu'elles sont destinées à être lues par un développeur dans un éditeur,
/// et non consommées par un programme.
/// </remarks>
public sealed record SourcePosition(int Line, int Column)
{
    /// <inheritdoc />
    public override string ToString() =>
        string.Create(null, stackalloc char[32], $"ligne {Line}, colonne {Column}");
}
