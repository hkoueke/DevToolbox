namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Le compte rendu de ce qui a été appliqué, écrit à côté de la copie optimisée.</summary>
/// <param name="RunId">L'identifiant de corrélation de l'exécution.</param>
/// <param name="InputPath">Le fichier d'origine.</param>
/// <param name="InputHash">Son empreinte.</param>
/// <param name="InputBytes">Sa taille.</param>
/// <param name="OutputPath">La copie publiée.</param>
/// <param name="OutputHash">Son empreinte.</param>
/// <param name="OutputBytes">Sa taille.</param>
/// <param name="Entries">Une entrée par transformation appliquée.</param>
/// <remarks>
/// Écrit dans la même étape que la copie, de sorte qu'une copie sans manifeste n'est pas un état que cet
/// outil puisse produire. Sans lui, un fichier optimisé serait un fichier dont personne ne sait ce qui lui
/// est arrivé.
/// </remarks>
public sealed record OptimizationManifest(
    Guid RunId,
    string InputPath,
    string InputHash,
    long InputBytes,
    string OutputPath,
    string OutputHash,
    long OutputBytes,
    IReadOnlyList<Transformation> Entries)
{
    /// <summary>Indique si tout ce qui a été appliqué se défait.</summary>
    public bool Reversible => Entries.All(entry => entry.Reversal.Reversible);

    /// <summary>Les octets effectivement gagnés, mesurés et non estimés.</summary>
    public long SavedBytes => InputBytes - OutputBytes;
}
