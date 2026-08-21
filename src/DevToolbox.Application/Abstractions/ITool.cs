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

    /// <summary>Exécute l'outil jusqu'au bout.</summary>
    /// <param name="cancellationToken">Annule l'exécution de manière coopérative.</param>
    /// <returns>L'exécution terminée, porteuse de son issue et de l'état de ses étapes.</returns>
    Task<ToolRun> RunAsync(CancellationToken cancellationToken);
}
