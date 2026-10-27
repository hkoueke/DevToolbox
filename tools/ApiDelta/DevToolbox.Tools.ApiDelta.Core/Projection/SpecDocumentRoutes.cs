using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Les routes qu'une production porte, et la version qu'elle annonce.</summary>
/// <param name="Routes">Les routes, « MÉTHODE /chemin », dans l'ordre du document.</param>
/// <param name="Version">La version annoncée, dont la projection suit la forme.</param>
/// <remarks>
/// Relevées une fois et déposées dans le contexte, plutôt que recalculées par chaque étape. La version
/// voyage avec elles parce que les deux viennent du même document et que les séparer ferait projeter
/// « nullable: true » contre une production en 3.1, où ce mot-clé n'existe plus.
/// </remarks>
public sealed record SpecDocumentRoutes(IReadOnlyList<string> Routes, OpenApiVersion Version)
{
    /// <summary>Relève les routes d'un document de production.</summary>
    /// <param name="document">Le document.</param>
    /// <returns>Ses routes et sa version.</returns>
    /// <remarks>
    /// Lues dans l'arbre de syntaxe, et non dans un modèle sémantique : c'est la seule représentation que
    /// cet outil compare, et la seule qui survive à une extension qu'il ne comprend pas.
    /// </remarks>
    public static SpecDocumentRoutes From(SpecDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<string> routes = [];

        if (document.Root is MappingNode root
            && Entry(root, "paths")?.Value is MappingNode paths)
        {
            foreach (MappingEntry path in paths.Entries)
            {
                if (path.Value is not MappingNode item)
                {
                    continue;
                }

                foreach (MappingEntry method in item.Entries)
                {
                    routes.Add(method.Key.Text.ToUpperInvariant() + " " + path.Key.Text);
                }
            }
        }

        return new SpecDocumentRoutes(routes, document.DeclaredVersion);
    }

    private static MappingEntry? Entry(MappingNode node, string key) =>
        node.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Key.Text, key, StringComparison.Ordinal));
}
