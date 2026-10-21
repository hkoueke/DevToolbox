namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>L'identité canonique de ce qu'une référence désigne.</summary>
/// <param name="File">Le fichier visé, vide pour une référence interne au document.</param>
/// <param name="Fragment">Le pointeur dans ce fichier.</param>
/// <remarks>
/// Canonique, parce que c'est la clé de l'ensemble des nœuds déjà visités : c'est elle qui fait terminer
/// la traversée d'un graphe cyclique. Deux écritures d'une même cible doivent donc produire la même
/// identité.
/// </remarks>
public sealed record ReferenceTarget(string File, string Fragment)
{
    /// <summary>Indique si la cible est hors du document courant.</summary>
    public bool IsExternal => File.Length > 0;

    /// <summary>Relit une référence écrite dans un document.</summary>
    /// <param name="reference">Le texte de la référence.</param>
    /// <returns>La cible, telle qu'elle sera comparée et visitée.</returns>
    public static ReferenceTarget Parse(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        int separator = reference.IndexOf('#', StringComparison.Ordinal);

        if (separator < 0)
        {
            return new ReferenceTarget(reference, string.Empty);
        }

        return new ReferenceTarget(reference[..separator], reference[(separator + 1)..]);
    }

    /// <inheritdoc />
    public override string ToString() => File + "#" + Fragment;
}
