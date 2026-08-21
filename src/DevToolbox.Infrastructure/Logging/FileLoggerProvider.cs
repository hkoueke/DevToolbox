using System.Globalization;
using System.Text;
using DevToolbox.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Infrastructure.Logging;

/// <summary>
/// Un puits de journalisation à rotation : un fichier par jour dans le dossier des journaux, avec une durée
/// de conservation bornée.
/// </summary>
/// <remarks>
/// Écrit à la main plutôt que pris comme dépendance, afin que le décorateur de masquage
/// <see cref="RedactingLoggerProvider"/> ne soit que du code <see cref="ILoggerProvider"/> ordinaire
/// au-dessus d'un rédacteur qui nous appartient. La sortie de journal ne va jamais sur la sortie standard
/// tant que Spectre occupe le terminal.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly AppPaths _paths;
    private readonly FileLoggerOptions _options;
    private readonly Lock _writeLock = new();
    private bool _disposed;

    /// <summary>Crée le fournisseur et applique sa politique de conservation.</summary>
    /// <param name="paths">Résout et confine le dossier des journaux.</param>
    /// <param name="options">Réglages de conservation et de niveau.</param>
    public FileLoggerProvider(AppPaths paths, FileLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);

        _paths = paths;
        _options = options;

        Directory.CreateDirectory(_paths.LogsDirectory);
        SweepExpired();
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose() => _disposed = true;

    /// <summary>Ajoute une ligne déjà mise en forme au fichier de journal du jour.</summary>
    /// <param name="line">La ligne à ajouter. L'appelant doit l'avoir déjà expurgée.</param>
    internal void Append(string line)
    {
        if (_disposed)
        {
            return;
        }

        string fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"devtoolbox-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");

        string path = AppPaths.CombineConfined(_paths.LogsDirectory, fileName);

        lock (_writeLock)
        {
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    /// <summary>Indique si un niveau doit être écrit.</summary>
    /// <param name="logLevel">Le niveau à tester.</param>
    /// <returns><see langword="true"/> si le niveau est actif.</returns>
    internal bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= _options.MinimumLevel;

    private void SweepExpired()
    {
        DateTime cutoff = DateTime.UtcNow - TimeSpan.FromDays(_options.RetentionDays);

        foreach (string path in Directory.EnumerateFiles(_paths.LogsDirectory, "devtoolbox-*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Un fichier encore ouvert par un autre processus DevToolbox est laissé en place et sera
                // nettoyé à un démarrage ultérieur. Refuser de démarrer parce qu'un vieux journal est
                // verrouillé serait un bien mauvais compromis.
            }
        }
    }
}
