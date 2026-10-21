using System.Security.Cryptography;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Réécrit la description de production sur place, et refuse de le faire dans tous les cas douteux.
/// </summary>
/// <remarks>
/// C'est la seule action de cet outil qu'on ne défait pas en supprimant un artefact, et toute sa
/// conception en découle. Quatre conditions, et aucune n'est négociable : il faut l'avoir demandé
/// explicitement ; il faut l'avoir confirmé, sur une invite qui nomme le fichier et la sauvegarde ; il
/// faut qu'une sauvegarde existe, sauf refus explicite ; et il faut que le fichier soit encore celui
/// qu'on a lu.
/// <para>
/// La dernière condition est la plus importante et la moins évidente. Entre la lecture et l'écriture, un
/// collègue a pu modifier le fichier, ou une branche a pu être changée. La réponse à une invite devenue
/// périmée n'est pas une autorisation d'écraser un contenu plus récent : l'empreinte est donc revérifiée
/// juste avant d'écrire, et non au moment de demander.
/// </para>
/// </remarks>
public sealed class InPlaceWriter : IInPlaceWriter
{
    private readonly IApiDeltaPrompt _prompt;
    private readonly ILogger<InPlaceWriter> _logger;

    /// <summary>Crée l'écrivain.</summary>
    /// <param name="prompt">Demande la confirmation qui nomme le fichier et la sauvegarde.</param>
    /// <param name="logger">Reçoit l'issue de chaque écriture.</param>
    public InPlaceWriter(IApiDeltaPrompt prompt, ILogger<InPlaceWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(logger);

        _prompt = prompt;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Artifact>> WriteAsync(
        SpecDocument production,
        ReadOnlyMemory<byte> content,
        bool backup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(production);

        string path = production.Path;
        string backupPath = path + ".sauvegarde";

        if (!_prompt.ConfirmInPlaceWrite(path, backup ? backupPath : string.Empty))
        {
            return Result.Fail<Artifact>(
                FailureReason.Cancelled,
                "La réécriture de « " + path + " » n'a pas été confirmée : rien n'a été modifié.");
        }

        if (await ChangedAsync(production, cancellationToken).ConfigureAwait(false) is { } stale)
        {
            return stale;
        }

        return await ReplaceAsync(path, backupPath, content, backup, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Vérifie que le fichier est encore celui qui a été lu, juste avant d'écrire.
    /// </summary>
    /// <returns>Un échec si le fichier a changé, <see langword="null"/> s'il est intact.</returns>
    private async Task<Result<Artifact>?> ChangedAsync(
        SpecDocument production,
        CancellationToken cancellationToken)
    {
        string current;

        try
        {
            current = Convert.ToHexStringLower(SHA256.HashData(
                await File.ReadAllBytesAsync(production.Path, cancellationToken)
                    .ConfigureAwait(false)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail<Artifact>(
                FailureReason.InvalidInput,
                "Le fichier n'a pas pu être relu avant d'être réécrit : " + exception.Message);
        }

        if (string.Equals(current, production.ContentHash, StringComparison.Ordinal))
        {
            return null;
        }

        Log.ArtifactRefused(_logger, production.Path, "le fichier a changé depuis sa lecture");

        return Result.Fail<Artifact>(
            FailureReason.InvalidInput,
            "Ce fichier a changé sur le disque depuis qu'il a été lu. Rien n'a été modifié : "
            + "relancez l'examen sur son contenu actuel.");
    }

    /// <summary>Met la sauvegarde de côté, puis remplace le fichier en une seule opération.</summary>
    private async Task<Result<Artifact>> ReplaceAsync(
        string path,
        string backupPath,
        ReadOnlyMemory<byte> content,
        bool backup,
        CancellationToken cancellationToken)
    {
        string temporary = path + ".partiel";

        try
        {
            if (backup)
            {
                File.Copy(path, backupPath, overwrite: true);
            }

            await File.WriteAllBytesAsync(temporary, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Discard(temporary);

            Log.ArtifactRefused(_logger, path, exception.Message);

            return Result.Fail<Artifact>(
                FailureReason.InvalidInput,
                "La réécriture n'a pas abouti : " + exception.Message);
        }
        catch (OperationCanceledException)
        {
            Discard(temporary);
            throw;
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(content.Span));

        Log.ArtifactWritten(_logger, path, content.Length, hash[..12]);

        return Result.Success(new Artifact(ArtifactKind.OptimizedDocument, path, content.Length, hash));
    }

    private static void Discard(string path)
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
            // Idem.
        }
    }
}
