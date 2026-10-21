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
    /// Combine un chemin relatif avec une racine et vérifie que le résultat ne s'en échappe pas.
    /// </summary>
    /// <param name="root">Le dossier sous lequel le résultat doit se trouver.</param>
    /// <param name="relativePath">Le chemin à ajouter, relatif à <paramref name="root"/>.</param>
    /// <returns>Le chemin complet, canonique.</returns>
    /// <exception cref="ArgumentException">
    /// Le chemin obtenu sort de <paramref name="root"/>, directement ou en traversant un lien.
    /// </exception>
    /// <remarks>
    /// La comparaison passe par <see cref="Path.GetRelativePath(string, string)"/> et non par un préfixe
    /// de chaîne : la racine <c>C:\out</c> a pour voisin <c>C:\outside</c>, dont le chemin commence par
    /// elle sans lui appartenir. Le contrôle porte ensuite sur la cible réelle des liens rencontrés, car
    /// un lien déplace la destination sans changer le texte du chemin. Les noms réservés de Windows sont
    /// écartés par là même : <c>NUL</c> se résout en chemin de périphérique, qui ne tombe sous aucune
    /// racine.
    /// </remarks>
    public static string CombineConfined(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string fullRoot = ResolveLinks(Path.GetFullPath(root));
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath));

        if (!IsConfinedTo(fullRoot, candidate)
            || !IsConfinedTo(fullRoot, ResolveNearestExisting(candidate)))
        {
            throw new ArgumentException(
                "Le chemin obtenu sort du dossier attendu.", nameof(relativePath));
        }

        return candidate;
    }

    private static bool IsConfinedTo(string root, string candidate)
    {
        if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string relative = Path.GetRelativePath(root, candidate);

        if (relative.Length == 0 || Path.IsPathRooted(relative))
        {
            // Enraciné : les deux chemins n'ont pas de racine commune, donc le candidat est ailleurs.
            return false;
        }

        return !string.Equals(relative, "..", StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith("../", StringComparison.Ordinal);
    }

    private static string ResolveLinks(string path)
    {
        FileSystemInfo? info = null;

        if (Directory.Exists(path))
        {
            info = new DirectoryInfo(path);
        }
        else if (File.Exists(path))
        {
            info = new FileInfo(path);
        }

        if (info is null)
        {
            return path;
        }

        return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName;
    }

    /// <summary>
    /// Résout les liens du plus proche ancêtre existant, puis réapplique le reste du chemin.
    /// </summary>
    /// <param name="candidate">Le chemin à examiner, qui n'existe pas forcément encore.</param>
    /// <returns>Le chemin tel qu'il se résoudra réellement.</returns>
    /// <remarks>
    /// Un artefact à écrire n'existe pas encore : il n'y a donc rien à résoudre sur lui, mais le dossier
    /// qui l'accueillera peut être un lien, et c'est lui qui décide où l'écriture atterrit.
    /// </remarks>
    private static string ResolveNearestExisting(string candidate)
    {
        string current = candidate;
        string remainder = string.Empty;

        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current) || File.Exists(current))
            {
                string resolved = ResolveLinks(current);

                return remainder.Length == 0
                    ? resolved
                    : Path.GetFullPath(Path.Combine(resolved, remainder));
            }

            string? parent = Path.GetDirectoryName(current);

            if (parent is null)
            {
                break;
            }

            remainder = Path.Combine(Path.GetFileName(current), remainder);
            current = parent;
        }

        return candidate;
    }
}
