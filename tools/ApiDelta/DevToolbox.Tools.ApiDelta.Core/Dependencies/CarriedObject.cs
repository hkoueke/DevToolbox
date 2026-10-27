using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>
/// L'objet complet qu'un correctif emporte pour exprimer une différence donnée.
/// </summary>
/// <remarks>
/// Cette règle existe ici une seule fois parce que deux lecteurs doivent en donner la même réponse :
/// l'écrivain, qui décide ce que la production consolidée contient, et le résolveur de dépendances, qui décide ce qu'il
/// faut emporter avec. Si les deux divergeaient d'un cran — l'un partant du champ modifié, l'autre de
/// l'opération qui le contient — le document référencerait des composants que la fermeture n'a jamais
/// visités, et il sortirait avec des références pendantes. C'est exactement la panne que cette règle
/// partagée empêche, et la raison pour laquelle elle n'est pas écrite deux fois.
/// </remarks>
public static class CarriedObject
{
    /// <summary>L'objet complet à emporter pour une différence située à cet emplacement.</summary>
    /// <param name="location">L'emplacement de la différence.</param>
    /// <returns>
    /// Le pointeur de l'objet complet, ou <see langword="null"/> si l'emplacement n'appartient à rien
    /// qu'une consolidation emporte — une entrée d'<c>info</c>, par exemple, dont seule la version est reprise.
    /// </returns>
    public static string? Of(SpecPointer location)
    {
        ArgumentNullException.ThrowIfNull(location);

        string[] segments = location.Json.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
        {
            return null;
        }

        if (string.Equals(segments[0], "components", StringComparison.Ordinal))
        {
            // Le composant entier : un schéma amputé du reste de ses propriétés ne se fusionne pas.
            return segments.Length >= 3
                ? "/components/" + segments[1] + "/" + segments[2]
                : null;
        }

        if (!string.Equals(segments[0], "paths", StringComparison.Ordinal))
        {
            return null;
        }

        // Une opération, quand la différence est dans une opération.
        if (segments.Length >= 3 && PathRows.Methods.Contains(segments[2]))
        {
            return "/paths/" + segments[1] + "/" + segments[2];
        }

        // Sinon l'élément de chemin entier. Un champ de ce niveau — « parameters », « servers » — vaut
        // pour chacune des opérations du chemin : le changer les change toutes, et un relecteur qui
        // fusionne un paramètre devenu obligatoire veut voir les opérations qu'il gouverne.
        return "/paths/" + segments[1];
    }

    /// <summary>Indique si un pointeur désigne un élément de chemin entier.</summary>
    /// <param name="carried">Le pointeur d'un objet emporté.</param>
    /// <returns><see langword="true"/> pour <c>/paths/&lt;chemin&gt;</c>, sans méthode.</returns>
    public static bool IsWholePathItem(string carried)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carried);

        return carried.StartsWith("/paths/", StringComparison.Ordinal)
            && carried.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2;
    }
}
