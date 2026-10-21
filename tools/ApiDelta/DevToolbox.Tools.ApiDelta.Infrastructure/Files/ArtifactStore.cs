using System.Security.Cryptography;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Écrit les artefacts d'une exécution, et rien d'autre.
/// </summary>
/// <remarks>
/// Trois précautions, chacune pour une raison précise. Le chemin est confiné sous le dossier de sortie, et
/// c'est le seul endroit de l'outil qui écrive. L'écriture passe par un nom temporaire puis un
/// déplacement, de sorte qu'une étape rejouée ou interrompue ne puisse pas laisser un rapport à moitié
/// écrit qui aurait l'air fini. Et l'artefact n'est consigné qu'une fois le fichier en place : un rapport
/// qui mentionnerait un fichier inexistant serait pire qu'un rapport muet.
/// </remarks>
public sealed class ArtifactStore : IArtifactStore
{
    private readonly IApiDeltaPrompt _prompt;
    private readonly ILogger<ArtifactStore> _logger;

    /// <summary>Crée le magasin.</summary>
    /// <param name="prompt">Demande confirmation avant d'écrire là où il ne faut pas écrire sans demander.</param>
    /// <param name="logger">Reçoit l'issue de chaque écriture.</param>
    public ArtifactStore(IApiDeltaPrompt prompt, ILogger<ArtifactStore> logger)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(logger);

        _prompt = prompt;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result> PrepareAsync(
        IReadOnlyList<string> fileNames,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentNullException.ThrowIfNull(selection);
        cancellationToken.ThrowIfCancellationRequested();

        string directory = Path.GetFullPath(selection.OutputDirectory);

        if (!Directory.Exists(directory))
        {
            return Task.FromResult(_prompt.ConfirmCreateDirectory(directory)
                ? Result.Success()
                : Result.Fail(
                    FailureReason.Cancelled,
                    "L'écriture dans « " + directory + " » n'a pas été confirmée."));
        }

        IReadOnlyList<string> present =
        [
            .. fileNames
                .Where(name => File.Exists(Path.Combine(directory, name)))
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        if (present.Count == 0 || _prompt.ConfirmReplace(present))
        {
            return Task.FromResult(Result.Success());
        }

        Log.ArtifactRefused(_logger, directory, "le remplacement n'a pas été confirmé");

        return Task.FromResult(Result.Fail(
            FailureReason.Cancelled,
            "Le remplacement des rapports déjà présents n'a pas été confirmé : rien n'a été écrit."));
    }

    /// <inheritdoc />
    public async Task<Result<Artifact>> PublishAsync(
        ArtifactKind kind,
        string fileName,
        ReadOnlyMemory<byte> content,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(selection);

        string directory = Path.GetFullPath(selection.OutputDirectory);

        string path;

        try
        {
            path = AppPaths.CombineConfined(directory, fileName);
        }
        catch (ArgumentException)
        {
            Log.ArtifactRefused(_logger, fileName, "le chemin obtenu sort du dossier de sortie");

            return Result.Fail<Artifact>(
                FailureReason.InvalidInput,
                "Le nom d'artefact « " + fileName + " » sort du dossier de sortie.");
        }

        if (!Directory.Exists(directory) && !_prompt.ConfirmCreateDirectory(directory))
        {
            return Result.Fail<Artifact>(
                FailureReason.Cancelled, "L'écriture dans « " + directory + " » n'a pas été confirmée.");
        }

        return await WriteAsync(kind, path, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Écrit sous un nom temporaire, puis met en place. Une étape rejouée ne peut ainsi pas laisser un
    /// fichier incomplet portant le nom d'un fichier fini.
    /// </summary>
    private async Task<Result<Artifact>> WriteAsync(
        ArtifactKind kind,
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        string temporary = path + ".partiel";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await File.WriteAllBytesAsync(temporary, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Delete(temporary);

            Log.ArtifactRefused(_logger, path, exception.Message);

            return Result.Fail<Artifact>(
                FailureReason.InvalidInput, "L'artefact n'a pas pu être écrit : " + exception.Message);
        }
        catch (OperationCanceledException)
        {
            // Annulée : le fichier temporaire part avec, pour qu'aucune trace ne subsiste d'un artefact
            // qui n'a jamais été produit.
            Delete(temporary);
            throw;
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(content.Span));

        Log.ArtifactWritten(_logger, path, content.Length, hash[..12]);

        return Result.Success(new Artifact(kind, path, content.Length, hash));
    }

    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Le nettoyage ne doit pas masquer la cause réelle de l'échec.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem : un fichier temporaire que l'on ne peut pas supprimer n'a pas à devenir l'échec
            // rapporté au développeur.
        }
    }
}
