using System.Text.Json;
using DevToolbox.Infrastructure.Platform;

namespace DevToolbox.Infrastructure.Storage;

/// <summary>Lit et écrit le fichier de réglages de la boîte à outils dans les données itinérantes.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AppPaths _paths;

    /// <summary>Crée le magasin.</summary>
    /// <param name="paths">Résout et confine le chemin des réglages.</param>
    public SettingsStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>Lit les réglages, en renvoyant les valeurs par défaut si le fichier manque ou est illisible.</summary>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Les réglages.</returns>
    public async Task<ToolboxSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            return new ToolboxSettings();
        }

        try
        {
            FileStream stream = File.OpenRead(_paths.SettingsFile);

            await using (stream.ConfigureAwait(false))
            {
                ToolboxSettings? settings = await JsonSerializer
                    .DeserializeAsync<ToolboxSettings>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);

                return settings ?? new ToolboxSettings();
            }
        }
        catch (JsonException)
        {
            // Un fichier de réglages corrompu est un souci de préférences, pas une panne. On repart des
            // valeurs par défaut.
            return new ToolboxSettings();
        }
        catch (IOException)
        {
            return new ToolboxSettings();
        }
    }

    /// <summary>Écrit les réglages.</summary>
    /// <param name="settings">Les réglages à conserver.</param>
    /// <param name="cancellationToken">Annule l'écriture.</param>
    /// <returns>Une tâche qui s'achève quand le fichier est écrit.</returns>
    public async Task SaveAsync(ToolboxSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_paths.SettingsDirectory);

        FileStream stream = File.Create(_paths.SettingsFile);

        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
