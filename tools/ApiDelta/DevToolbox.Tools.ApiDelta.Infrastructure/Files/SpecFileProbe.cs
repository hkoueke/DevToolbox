using System.Security.Cryptography;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Établit qu'un chemin désigne une description lisible, et en prend l'empreinte.
/// </summary>
/// <remarks>
/// Toutes les issues prévisibles sont des valeurs et non des exceptions : un fichier absent, un dossier
/// donné à la place d'un fichier, un chemin trop long, un accès refusé, un fichier vide, un fichier trop
/// volumineux. Un développeur qui se trompe de chemin doit lire une phrase, pas une trace d'appel.
/// <para>
/// Chaque refus passe par <see cref="Refuse"/>, de sorte qu'aucun ne puisse être rendu sans être consigné.
/// Ce qui est consigné reste extérieur au contenu : un chemin, une taille, le début d'une empreinte.
/// </para>
/// </remarks>
public sealed class SpecFileProbe : ISpecFileProbe
{
    private readonly ILogger<SpecFileProbe> _logger;

    /// <summary>Crée la sonde.</summary>
    /// <param name="logger">Reçoit le verdict de chaque fichier examiné.</param>
    public SpecFileProbe(ILogger<SpecFileProbe> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<SpecFileFacts>> ProbeAsync(
        string path,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException
            or NotSupportedException)
        {
            return Refuse(path, "ce n'est pas un chemin de fichier valide");
        }

        if (Directory.Exists(fullPath))
        {
            return Refuse(fullPath, "ce chemin désigne un dossier, pas une description");
        }

        if (!File.Exists(fullPath))
        {
            return Refuse(fullPath, "ce fichier n'existe pas");
        }

        long length = new FileInfo(fullPath).Length;

        if (length == 0)
        {
            return Refuse(fullPath, "ce fichier est vide");
        }

        if (length > maxBytes)
        {
            return Refuse(
                fullPath,
                $"ce fichier pèse {length} octets, au-delà de la limite de {maxBytes}");
        }

        return await ComputeAsync(fullPath, length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Prend l'empreinte du fichier, en le lisant en flux plutôt qu'en mémoire.</summary>
    private async Task<Result<SpecFileFacts>> ComputeAsync(
        string fullPath,
        long length,
        CancellationToken cancellationToken)
    {
        try
        {
            FileStream stream = File.OpenRead(fullPath);

            await using (stream.ConfigureAwait(false))
            {
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                string hex = Convert.ToHexStringLower(hash);

                Log.FileProbed(_logger, fullPath, length, hex[..12]);

                return Result.Success(new SpecFileFacts(fullPath, length, hex));
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Refuse(fullPath, "ce fichier existe mais ne peut pas être lu", FailureReason.PermissionDenied);
        }
        catch (IOException exception)
        {
            return Refuse(fullPath, "ce fichier n'a pas pu être lu : " + exception.Message);
        }
    }

    /// <summary>Consigne un refus et le rend sous forme de valeur.</summary>
    private Result<SpecFileFacts> Refuse(
        string path,
        string reason,
        FailureReason failure = FailureReason.InvalidInput)
    {
        Log.FileRefused(_logger, path, reason);

        return Result.Fail<SpecFileFacts>(failure, char.ToUpperInvariant(reason[0]) + reason[1..] + " : " + path);
    }
}
