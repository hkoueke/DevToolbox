using System.Text.Json;
using DevToolbox.Application.Abstractions;
using DevToolbox.Infrastructure.Internal;
using DevToolbox.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Infrastructure.Storage;

/// <summary>
/// Conserve les exécutions interrompues sous forme d'un fichier JSON par exécution, dans les données
/// d'application locales.
/// </summary>
/// <remarks>
/// L'invariant qui compte ici est ce qui est <em>absent</em> : <see cref="PersistedRun"/> n'offre aucun
/// chemin vers une valeur de variable, si bien qu'aucune valeur ne peut être écrite, même par mégarde. Le
/// garde-fou d'exécution ci-dessous est une seconde ligne de défense au cas où ce type serait un jour élargi.
/// </remarks>
public sealed class FileRunCheckpointStore : IRunCheckpointStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly Lazy<bool> ShapeVerified = new(VerifyCheckpointShape);

    private readonly AppPaths _paths;
    private readonly IClock _clock;
    private readonly ILogger<FileRunCheckpointStore> _logger;

    /// <summary>Crée le magasin.</summary>
    /// <param name="paths">Résout et confine le dossier des exécutions.</param>
    /// <param name="clock">Fournit l'heure courante, pour la péremption.</param>
    /// <param name="logger">Reçoit l'issue des écritures et des nettoyages.</param>
    public FileRunCheckpointStore(AppPaths paths, IClock clock, ILogger<FileRunCheckpointStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveAsync(PersistedRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        GuardAgainstValues(run);

        Directory.CreateDirectory(_paths.RunsDirectory);
        string path = AppPaths.CombineConfined(_paths.RunsDirectory, FileNameFor(run.RunId));

        FileStream stream = File.Create(path);

        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, run, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        Log.CheckpointSaved(_logger, run.RunId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersistedRun>> ListResumableAsync(
        string toolId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        if (!Directory.Exists(_paths.RunsDirectory))
        {
            return [];
        }

        List<PersistedRun> runs = [];

        foreach (string path in Directory.EnumerateFiles(_paths.RunsDirectory, "*.json"))
        {
            PersistedRun? run = await TryReadAsync(path, cancellationToken).ConfigureAwait(false);

            if (run is not null && string.Equals(run.ToolId, toolId, StringComparison.Ordinal))
            {
                runs.Add(run);
            }
        }

        return runs.OrderByDescending(run => run.LastUpdatedAt).ToList();
    }

    /// <inheritdoc />
    public Task DiscardAsync(Guid runId, CancellationToken cancellationToken)
    {
        string path = AppPaths.CombineConfined(_paths.RunsDirectory, FileNameFor(runId));

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int> SweepExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_paths.RunsDirectory))
        {
            return 0;
        }

        DateTimeOffset cutoff = _clock.UtcNow - maxAge;
        int removed = 0;

        foreach (string path in Directory.EnumerateFiles(_paths.RunsDirectory, "*.json"))
        {
            PersistedRun? run = await TryReadAsync(path, cancellationToken).ConfigureAwait(false);

            // Un point de reprise illisible ou malformé est nettoyé lui aussi. Il ne peut pas être repris,
            // et le conserver ne ferait qu'offrir au développeur une entrée qui échoue quand il la choisit.
            if (run is null || run.LastUpdatedAt < cutoff)
            {
                File.Delete(path);
                removed++;
            }
        }

        if (removed > 0)
        {
            Log.CheckpointsSwept(_logger, removed);
        }

        return removed;
    }

    private static string FileNameFor(Guid runId) =>
        string.Concat(runId.ToString("D", System.Globalization.CultureInfo.InvariantCulture), ".json");

    /// <summary>
    /// Vérifie que la forme du point de reprise n'a pas été élargie au point de porter une valeur.
    /// </summary>
    /// <remarks>
    /// Aujourd'hui aucune valeur ne peut atteindre le disque, car <see cref="PersistedRun"/> n'expose que
    /// des identifiants, des noms, des noms d'étapes et des horodatages. Le risque contre lequel ce
    /// garde-fou protège est une modification future ajoutant un membre qui mène à un cliché ou à une
    /// entrée de variable. Plutôt que d'inspecter les données — ce qui ne prouverait rien, une instance sans
    /// valeur d'un type porteur de valeurs passant tout aussi bien — il inspecte le <em>type</em>, une seule
    /// fois, et refuse purement et simplement d'écrire si la forme a dérivé.
    /// </remarks>
    private static void GuardAgainstValues(PersistedRun run)
    {
        _ = run;

        if (!ShapeVerified.Value)
        {
            throw new InvalidOperationException(
                "The run checkpoint type has been widened beyond identifiers, names and timestamps. "
                + "Refusing to write, because a variable value could now reach disk.");
        }
    }

    private static bool VerifyCheckpointShape()
    {
        HashSet<Type> permitted =
        [
            typeof(Guid),
            typeof(string),
            typeof(int),
            typeof(DateTimeOffset),
            typeof(IReadOnlyList<string>),
            typeof(IReadOnlyList<PersistedGroupSelection>),
        ];

        return IsShapePermitted(typeof(PersistedRun), permitted)
            && IsShapePermitted(typeof(PersistedGroupSelection), permitted);
    }

    private static bool IsShapePermitted(Type type, HashSet<Type> permitted) =>
        type.GetProperties(global::System.Reflection.BindingFlags.Public
                | global::System.Reflection.BindingFlags.Instance)
            .All(property => permitted.Contains(property.PropertyType));

    private static async Task<PersistedRun?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            FileStream stream = File.OpenRead(path);

            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer
                    .DeserializeAsync<PersistedRun>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
