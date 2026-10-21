using System.Globalization;
using System.Text;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Écrit le manifeste d'une optimisation appliquée.
/// </summary>
/// <remarks>
/// Le manifeste dit ce qui <em>a été</em> fait, là où le plan disait ce qui pourrait l'être. Les deux
/// coexistent délibérément : relire le plan après coup montre ce qui a été écarté, et le manifeste montre
/// ce qui a été retenu. Un seul des deux fichiers laisserait la différence invisible.
/// </remarks>
public static class ManifestWriter
{
    /// <summary>Écrit le manifeste.</summary>
    /// <param name="manifest">Ce qui a été appliqué.</param>
    /// <returns>Le manifeste, en octets.</returns>
    public static ReadOnlyMemory<byte> Write(OptimizationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        StringBuilder builder = new();

        builder.AppendLine(
            "# Ce qui a été appliqué à la copie optimisée. L'original n'a pas été modifié.");
        builder.Append("runId: ").AppendLine(manifest.RunId.ToString());

        Side(builder, "input", manifest.InputPath, manifest.InputHash, manifest.InputBytes);
        Side(builder, "output", manifest.OutputPath, manifest.OutputHash, manifest.OutputBytes);

        builder.Append("savedBytes: ").AppendLine(Number(manifest.SavedBytes));
        builder.Append("reversible: ").AppendLine(manifest.Reversible ? "true" : "false");

        Entries(builder, manifest);

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void Side(
        StringBuilder builder,
        string heading,
        string path,
        string hash,
        long bytes)
    {
        builder.Append(heading).AppendLine(":");
        builder.Append("  path: ").AppendLine(Quote(path));
        builder.Append("  sha256: ").AppendLine(Quote(hash));
        builder.Append("  bytes: ").AppendLine(Number(bytes));
    }

    private static void Entries(StringBuilder builder, OptimizationManifest manifest)
    {
        builder.AppendLine("entries:");

        if (manifest.Entries.Count == 0)
        {
            builder.AppendLine("  []");
            return;
        }

        foreach (Transformation entry in manifest.Entries)
        {
            builder.Append("  - ruleId: ").AppendLine(entry.RuleId);
            builder.Append("    safety: ").AppendLine(Safety(entry));
            builder.Append("    evidence: ")
                .AppendLine(entry.Evidence is { } evidence ? Quote("sha256:" + evidence) : "null");
            builder.Append("    origins: [").Append(
                string.Join(", ", entry.Origins.Select(origin => Quote(origin.Json)))).AppendLine("]");
            builder.Append("    destination: ")
                .AppendLine(entry.Destination is { } destination ? Quote(destination.Json) : "null");
            builder.Append("    reversal: ").AppendLine(Quote(entry.Reversal.Text));
        }
    }

    /// <summary>La classe de sûreté, dans le vocabulaire publié.</summary>
    private static string Safety(Transformation entry) => entry.Safety switch
    {
        Audit.SafetyClass.Safe => "SAFE",
        Audit.SafetyClass.ReviewRequired => "REVIEW_REQUIRED",
        _ => "UNSAFE",
    };

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Quote(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
