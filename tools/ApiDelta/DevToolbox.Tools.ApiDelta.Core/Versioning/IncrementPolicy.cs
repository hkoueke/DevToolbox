using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;

namespace DevToolbox.Tools.ApiDelta.Core.Versioning;

/// <summary>
/// Traduit l'effet d'un changement en incrément, en laissant à la politique ce qui lui revient.
/// </summary>
/// <remarks>
/// La règle de base est celle du versionnage sémantique : ce qui casse commande un majeur, ce qui ajoute un
/// mineur, ce qui ne touche qu'à la documentation un correctif.
/// <para>
/// Deux cas échappent à la spécification et relèvent d'un choix. Une valeur d'énumération ajoutée ne
/// refuse rien à personne, mais un client engendré qui traite exhaustivement une énumération cesse de
/// compiler ; une réponse ajoutée est dans le même cas. L'outil ne tranche donc pas : il applique ce que
/// l'équipe a décidé, et le rapport dit laquelle de ces décisions était en vigueur.
/// </para>
/// </remarks>
internal static class IncrementPolicy
{
    internal static VersionImpact Of(Difference difference, VersioningPolicy policy)
    {
        if (difference.Policy is { } key && difference.Kind == DifferenceKind.Added)
        {
            return Resolve(key, difference, policy);
        }

        return difference.Impact switch
        {
            ChangeImpact.Breaking => VersionImpact.Major,
            ChangeImpact.NonBreaking => VersionImpact.Minor,
            ChangeImpact.DocumentationOnly => VersionImpact.Patch,

            // Ce que l'outil n'a pas su classer ne peut pas valoir un correctif : quelque chose a changé,
            // et ce n'était pas de la documentation. Il ne vaut pas non plus un majeur à lui seul — une
            // extension de fournisseur modifiée ferait alors publier une version majeure, et l'équipe
            // apprendrait à ne plus lire la recommandation.
            _ => VersionImpact.Minor,
        };
    }

    /// <summary>
    /// Les choix de politique tels qu'ils sont résolus pour cette exécution, à consigner dans le rapport.
    /// </summary>
    internal static IReadOnlyDictionary<string, VersionImpact> Resolved(VersioningPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new Dictionary<string, VersionImpact>(StringComparer.Ordinal)
        {
            [VersioningPolicyKeys.EnumValueAdded] = policy.EnumValueAdded,
            ["successResponseAdded"] = policy.SuccessResponseAdded,
            ["errorResponseAdded"] = policy.ErrorResponseAdded,
        };
    }

    private static VersionImpact Resolve(
        string key,
        Difference difference,
        VersioningPolicy policy)
    {
        if (string.Equals(key, VersioningPolicyKeys.EnumValueAdded, StringComparison.Ordinal))
        {
            return policy.EnumValueAdded;
        }

        if (!string.Equals(key, VersioningPolicyKeys.ResponseAdded, StringComparison.Ordinal))
        {
            return VersionImpact.Minor;
        }

        // Une réponse de succès ajoutée ne vaut pas une réponse d'erreur ajoutée : la première peut
        // changer ce qu'un appelant reçoit en temps normal. Le code de statut est le dernier segment du
        // pointeur, et c'est lui qui le dit.
        return IsSuccess(difference) ? policy.SuccessResponseAdded : policy.ErrorResponseAdded;
    }

    private static bool IsSuccess(Difference difference)
    {
        string[] segments = difference.Location.Json.Split(
            '/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length > 0 && segments[^1].StartsWith('2');
    }
}
