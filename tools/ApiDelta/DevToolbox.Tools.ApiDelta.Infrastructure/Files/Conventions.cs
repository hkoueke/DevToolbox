namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Trouve un fichier de configuration nommé par convention à côté d'une description.
/// </summary>
/// <remarks>
/// À côté, et non dans un dossier de configuration quelque part : un jeu de règles appartient à la
/// description qu'il gouverne, et les deux voyagent ensemble dans le même dépôt. La découverte est une
/// commodité, pas une obligation — le développeur peut toujours désigner un autre fichier, et l'absence
/// de fichier n'est pas un défaut.
/// </remarks>
internal static class Conventions
{
    /// <summary>Le fichier portant ce nom à côté d'une description, s'il existe.</summary>
    /// <param name="descriptionPath">Le fichier en service.</param>
    /// <param name="fileName">Le nom cherché.</param>
    /// <returns>Le chemin complet, ou <see langword="null"/>.</returns>
    internal static string? Beside(string descriptionPath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(descriptionPath))
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(descriptionPath));

        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        string candidate = Path.Combine(directory, fileName);

        return File.Exists(candidate) ? candidate : null;
    }
}
