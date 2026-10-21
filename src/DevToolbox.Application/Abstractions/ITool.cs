using DevToolbox.Domain.Runs;

namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Un outil proposé par la boîte à outils. Le shell construit ses onglets et ses menus en énumérant les
/// implémentations enregistrées : ajouter un outil ne demande donc de modifier aucun outil existant.
/// </summary>
public interface ITool
{
    /// <summary>Un identifiant stable, par exemple <c>varcompare</c>. Utilisé dans les journaux et les fichiers de reprise.</summary>
    string Id { get; }

    /// <summary>Le nom affiché dans le menu.</summary>
    string DisplayName { get; }

    /// <summary>L'onglet sous lequel l'outil apparaît, par exemple <c>Azure DevOps</c>.</summary>
    string Tab { get; }

    /// <summary>Une description d'une ligne affichée à côté du nom.</summary>
    string Description { get; }

    /// <summary>Indique si l'outil a besoin que le serveur soit configuré pour fonctionner.</summary>
    /// <remarks>
    /// Membre par défaut, et c'est délibéré : les outils adressés à Azure DevOps n'ont rien à déclarer,
    /// seul celui qui ne parle à personne le dit. La coquille ne résout et ne demande l'adresse du serveur
    /// qu'au moment où un outil qui en a besoin est choisi — sans quoi auditer un fichier OpenAPI local
    /// exigerait d'inventer une adresse de serveur.
    /// </remarks>
    bool RequiresServer => true;

    /// <summary>Exécute l'outil jusqu'au bout.</summary>
    /// <param name="cancellationToken">Annule l'exécution de manière coopérative.</param>
    /// <returns>L'exécution terminée, porteuse de son issue et de l'état de ses étapes.</returns>
    Task<ToolRun> RunAsync(CancellationToken cancellationToken);
}
