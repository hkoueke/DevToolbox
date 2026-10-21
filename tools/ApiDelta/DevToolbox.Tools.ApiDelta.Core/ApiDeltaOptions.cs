using DevToolbox.Tools.ApiDelta.Core.Configuration;

namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Ce que la racine de composition fixe pour apidelta.</summary>
/// <param name="DefaultRuleset">
/// Le jeu de règles en vigueur à défaut d'un fichier. Les valeurs par défaut forment une configuration
/// utilisable : l'outil sert avant que quiconque ait écrit un jeu de règles.
/// </param>
/// <param name="RunRetention">
/// Combien de temps une exécution interrompue reste reprenable. Aligné sur la politique de la boîte à
/// outils plutôt que redéfini ici.
/// </param>
/// <remarks>
/// Aucun degré de parallélisme ici, et ce n'est pas un oubli : apidelta n'émet aucune requête. Son coût
/// est un parcours d'arbre, que l'on fait une fois.
/// </remarks>
public sealed record ApiDeltaOptions(Ruleset DefaultRuleset, TimeSpan RunRetention)
{
    /// <summary>Les options par défaut.</summary>
    public static ApiDeltaOptions Default { get; } = new(Ruleset.Default, TimeSpan.FromDays(7));
}
