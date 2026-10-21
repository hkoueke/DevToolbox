using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Une règle d'optimisation : ce qui sait exécuter une transformation proposée.</summary>
/// <remarks>
/// Une règle n'invente rien : elle reçoit une transformation que le plan a chiffrée et rend l'arbre que
/// cette transformation produit. Séparer les deux est ce qui permet au plan d'être publié, relu et
/// discuté avant qu'une seule édition ait lieu — et ce qui garantit qu'aucune édition n'a lieu qui ne
/// figure au plan.
/// </remarks>
public interface IOptimizationRule
{
    /// <summary>L'identifiant de la règle, qui fait partie de la sortie et ne change pas.</summary>
    string RuleId { get; }

    /// <summary>
    /// Indique si la règle est active à défaut de réglage.
    /// </summary>
    /// <remarks>
    /// Deux règles valent <see langword="false"/> ici, et ce n'est pas de la prudence décorative :
    /// extraire un schéma écrit sur place renomme des types chez les appelants, et poser un drapeau de
    /// dépréciation change ce que la description publie. Les deux sont souhaitables et aucune ne doit
    /// arriver sans qu'on l'ait demandée.
    /// </remarks>
    bool EnabledByDefault { get; }

    /// <summary>
    /// Indique si la règle fait disparaître ce qu'elle vise, au lieu de le déplacer.
    /// </summary>
    /// <remarks>
    /// Déclaratif, parce que la protection des extensions n'a pas le même critère dans les deux cas :
    /// un déplacement laisse la donnée dans le document, une suppression la rend introuvable.
    /// </remarks>
    bool Removes { get; }

    /// <summary>Applique une transformation à un arbre.</summary>
    /// <param name="root">La racine avant transformation.</param>
    /// <param name="transformation">Ce que le plan a proposé.</param>
    /// <returns>La racine après, ou de quoi expliquer pourquoi la transformation n'a pas eu lieu.</returns>
    Result<SpecNode> Apply(SpecNode root, Transformation transformation);
}
