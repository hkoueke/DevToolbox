using System.Runtime.CompilerServices;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Vérifie la taille avant allocation et borne la lecture, même si le fichier grandit.</summary>
internal static class BoundedDocumentReader
{
    private const int BufferSize = 64 * 1024;

    internal static byte[]? Read(string path, long maximumBytes)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.SequentialScan);
        return Read(stream, maximumBytes);
    }

    internal static byte[]? Read(Stream stream, long maximumBytes)
    {
        long limit = Math.Min(maximumBytes, Array.MaxLength);
        if (stream.Length > limit)
        {
            return null;
        }

        using MemoryStream content = new();
        byte[] buffer = new byte[(int)Math.Min(limit, BufferSize)];
        while (content.Length < limit)
        {
            int count = (int)Math.Min(buffer.Length, limit - content.Length);
            int read = stream.Read(buffer, 0, count);
            if (read == 0)
            {
                return content.ToArray();
            }

            content.Write(buffer, 0, read);
        }

        // Un seul octet sentinelle distingue la taille exacte d'un dépassement.
        return stream.ReadByte() == -1 ? content.ToArray() : null;
    }

    internal static async Task<byte[]?> ReadAsync(string path, long maximumBytes, CancellationToken cancellationToken)
    {
        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using ConfiguredAsyncDisposable _ = stream.ConfigureAwait(false);
        return await ReadAsync(stream, maximumBytes, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]?> ReadAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long limit = Math.Min(maximumBytes, Array.MaxLength);
        if (stream.Length > limit)
        {
            return null;
        }

        using MemoryStream content = new();
        byte[] buffer = new byte[(int)Math.Max(1, Math.Min(limit, BufferSize))];
        while (content.Length < limit)
        {
            int count = (int)Math.Min(buffer.Length, limit - content.Length);
            int read = await stream.ReadAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return content.ToArray();
            }

            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        int extra = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        return extra == 0 ? content.ToArray() : null;
    }
}
