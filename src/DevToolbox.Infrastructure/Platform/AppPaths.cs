namespace DevToolbox.Infrastructure.Platform;

/// <summary>
/// Résout les dossiers dans lesquels DevToolbox écrit, et confine sous eux tout chemin qui en dérive.
/// </summary>
/// <remarks>
/// Les chemins passent par <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> plutôt que
/// d'être assemblés à partir de variables d'environnement, et chaque fichier ouvert par l'application est
/// vérifié comme se trouvant bien sous la racine attendue.
/// </remarks>
public sealed class AppPaths
{
    private const string ProductFolder = "DevToolbox";

    /// <summary>Crée le résolveur de chemins pour le vrai profil de l'utilisateur connecté.</summary>
    public AppPaths()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    /// <summary>Crée le résolveur de chemins sous des racines explicites.</summary>
    /// <param name="roamingRoot">La racine des données d'application itinérantes.</param>
    /// <param name="localRoot">La racine des données d'application locales.</param>
    /// <remarks>
    /// Les racines sont des paramètres plutôt que d'être lues depuis l'environnement à l'intérieur de cette
    /// classe, afin qu'un test puisse diriger toute l'application vers un dossier temporaire.
    /// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> passe par l'API shell de Windows
    /// et ne suit pas la variable <c>LOCALAPPDATA</c> : rediriger l'environnement n'aurait donc rien donné.
    /// </remarks>
    public AppPaths(string roamingRoot, string localRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roamingRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);

        SettingsDirectory = Path.Combine(roamingRoot, ProductFolder);
        RunsDirectory = Path.Combine(localRoot, ProductFolder, "runs");
        LogsDirectory = Path.Combine(localRoot, ProductFolder, "logs");
    }

    /// <summary>Où résident les réglages.</summary>
    public string SettingsDirectory { get; }

    /// <summary>Où résident les points de reprise des exécutions interrompues.</summary>
    public string RunsDirectory { get; }

    /// <summary>Où résident les fichiers de journal à rotation.</summary>
    public string LogsDirectory { get; }

    /// <summary>Le fichier de réglages.</summary>
    public string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>Crée, si besoin, chaque dossier dans lequel l'application écrit.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(RunsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>
    /// Combine un nom de fichier avec une racine et vérifie que le résultat ne s'en échappe pas.
    /// </summary>
    /// <param name="root">Le dossier sous lequel le fichier doit se trouver.</param>
    /// <param name="fileName">Le nom de fichier à ajouter.</param>
    /// <returns>Le chemin complet.</returns>
    /// <exception cref="ArgumentException">Le chemin obtenu s'échappe de <paramref name="root"/>.</exception>
    public static string CombineConfined(string root, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string fullRoot = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, fileName));

        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The resolved path escapes the DevToolbox directory.", nameof(fileName));
        }

        return candidate;
    }
}
