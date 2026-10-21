using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Configuration;

namespace DevToolbox.Tools.ApiDelta.Core.Versioning;

/// <summary>Une version sémantique, telle qu'une description la déclare.</summary>
/// <param name="Major">Le rang majeur.</param>
/// <param name="Minor">Le rang mineur.</param>
/// <param name="Patch">Le rang de correctif.</param>
/// <param name="PreRelease">L'étiquette de pré-publication, le cas échéant.</param>
/// <param name="Build">Les métadonnées de construction, le cas échéant.</param>
/// <remarks>
/// Lue, jamais devinée. Une version que l'outil ne sait pas lire est signalée comme telle : inventer un
/// point de départ reviendrait à recommander un numéro à partir de rien, ce qui est précisément la chose
/// qu'un développeur ne peut pas vérifier d'un coup d'œil.
/// </remarks>
public sealed record SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? PreRelease = null,
    string? Build = null)
{
    /// <summary>Relit une version.</summary>
    /// <param name="text">Le texte déclaré.</param>
    /// <param name="version">La version relue.</param>
    /// <returns><see langword="true"/> si le texte est une version sémantique.</returns>
    public static bool TryParse(string? text, out SemanticVersion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string core = text.Trim();
        string? build = null;
        string? preRelease = null;

        int plus = core.IndexOf('+', StringComparison.Ordinal);

        if (plus >= 0)
        {
            build = core[(plus + 1)..];
            core = core[..plus];
        }

        int dash = core.IndexOf('-', StringComparison.Ordinal);

        if (dash >= 0)
        {
            preRelease = core[(dash + 1)..];
            core = core[..dash];
        }

        string[] parts = core.Split('.');

        if (parts.Length != 3
            || !TryRank(parts[0], out int major)
            || !TryRank(parts[1], out int minor)
            || !TryRank(parts[2], out int patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, preRelease, build);

        return true;
    }

    /// <summary>Applique un incrément.</summary>
    /// <param name="increment">L'incrément à appliquer.</param>
    /// <returns>La version suivante.</returns>
    /// <remarks>
    /// Les rangs inférieurs repartent de zéro, et l'étiquette de pré-publication comme les métadonnées de
    /// construction tombent : elles appartenaient à la version précédente, et les traîner ferait publier
    /// « 3.0.0-rc1 » pour une version qui n'est pas une candidate.
    /// </remarks>
    public SemanticVersion Increment(VersionImpact increment) => increment switch
    {
        VersionImpact.Major => new SemanticVersion(Major + 1, 0, 0),
        VersionImpact.Minor => new SemanticVersion(Major, Minor + 1, 0),
        VersionImpact.Patch => new SemanticVersion(Major, Minor, Patch + 1),
        _ => this,
    };

    /// <summary>Compare deux versions sur leurs trois rangs.</summary>
    /// <param name="other">L'autre version.</param>
    /// <returns>Négatif, nul ou positif, selon l'ordre.</returns>
    /// <remarks>
    /// Les trois rangs seuls : l'ordre des pré-publications a ses propres règles, et cet outil n'a pas
    /// besoin de les connaître pour dire qu'une version proposée est inférieure à celle qu'il recommande.
    /// </remarks>
    public int CompareRanks(SemanticVersion other)
    {
        ArgumentNullException.ThrowIfNull(other);

        int major = Major.CompareTo(other.Major);

        if (major != 0)
        {
            return major;
        }

        int minor = Minor.CompareTo(other.Minor);

        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        string core = string.Create(
            CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

        if (PreRelease is { } preRelease)
        {
            core += "-" + preRelease;
        }

        return Build is { } build ? core + "+" + build : core;
    }

    private static bool TryRank(string text, out int rank) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out rank);
}
