namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Confinement lexical avec refus des liens symboliques et jonctions sur le trajet.</summary>
internal static class ImportConfinement
{
    internal static bool Contains(string directory, string candidate)
    {
        string root = Path.GetFullPath(directory);
        string full = Path.GetFullPath(candidate);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!string.Equals(root, full, comparison) && !full.StartsWith(prefix, comparison))
        {
            return false;
        }

        string current = Path.GetPathRoot(full)!;
        foreach (string segment in full[current.Length..].Split(Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                {
                    return false;
                }
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                // Missing paths are diagnosed as unresolved imports by the caller.
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        return true;
    }
}
