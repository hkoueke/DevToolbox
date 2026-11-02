using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Operations;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Valide ce que le développeur a tapé, avant que cela serve à quoi que ce soit.</summary>
/// <remarks>
/// Avant, et non au moment d'écrire. Deux raisons, et la seconde vient d'une passe STRIDE.
/// <para>
/// La première est d'usage : un refus au moment d'écrire arrive trop tard pour être utile, et laisse une
/// valeur mal formée traverser le plan et le rapport d'abord.
/// </para>
/// <para>
/// La seconde touche à la sûreté. Les segments d'un <c>SpecPointer</c> sont échappés au format JSON
/// Pointer, où <c>~</c> et <c>/</c> ont un sens. Un chemin portant ces caractères aux mauvais endroits
/// construirait donc un pointeur qui désigne un autre nœud que celui visé — et l'épissure écrirait là. Ce
/// n'est pas théorique : un gabarit de chemin contient des <c>/</c> par construction. Valider d'abord est
/// ce qui garantit qu'aucune valeur mal formée n'atteint la construction d'un pointeur.
/// </para>
/// <para>
/// Aucune règle ne répare l'entrée. Un chemin corrigé en silence serait un chemin que le développeur n'a
/// pas choisi, ce qui est exactement ce que cette fonctionnalité existe pour empêcher.
/// </para>
/// </remarks>
public static class RouteSyntax
{
    /// <summary>Les méthodes qu'OpenAPI définit pour un élément de chemin.</summary>
    private static readonly string[] Methods =
        ["GET", "PUT", "POST", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE"];

    /// <summary>Valide un gabarit de chemin.</summary>
    /// <param name="typed">Ce que le développeur a tapé.</param>
    /// <returns>Le chemin, ou un refus qui nomme la règle enfreinte.</returns>
    public static Result<string> Path(string typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return Refuse("un chemin ne peut pas être vide");
        }

        string path = typed.Trim();

        if (path[0] != '/')
        {
            return Refuse("un chemin commence par « / »");
        }

        if (path.Any(char.IsWhiteSpace))
        {
            return Refuse("un chemin ne contient pas d'espace");
        }

        if (path.Contains("//", StringComparison.Ordinal))
        {
            return Refuse("un chemin ne contient pas de segment vide");
        }

        if (path.Contains('~', StringComparison.Ordinal))
        {
            // La règle venue de la passe STRIDE. « ~ » est le caractère d'échappement du format JSON
            // Pointer : un chemin qui en porte construirait un pointeur désignant un autre nœud.
            return Refuse("un chemin ne contient pas « ~ », qui a un sens dans un pointeur");
        }

        return Braces(path) is { } refusal ? refusal : Result.Success(path);
    }

    /// <summary>Valide une méthode HTTP.</summary>
    /// <param name="typed">Ce que le développeur a tapé.</param>
    /// <returns>La méthode, ou un refus listant ce qu'OpenAPI définit.</returns>
    public static Result<HttpOperationMethod> Method(string typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return Result.Fail<HttpOperationMethod>(
                FailureReason.InvalidInput,
                "Aucun verbe n'a été indiqué. OpenAPI définit : " + string.Join(", ", Methods) + ".");
        }

        string method = typed.Trim().ToUpperInvariant();

        return Enum.TryParse(method, ignoreCase: true, out HttpOperationMethod parsed)
            && Methods.Contains(method, StringComparer.Ordinal)
            ? Result.Success(parsed)
            : Result.Fail<HttpOperationMethod>(
                FailureReason.InvalidInput,
                "« " + typed.Trim() + " » n'est pas un verbe qu'OpenAPI définit pour un chemin. Ceux "
                + "qu'il définit : " + string.Join(", ", Methods) + ".");
    }

    /// <summary>Les noms des paramètres de chemin, dans l'ordre du gabarit.</summary>
    /// <param name="path">Un chemin déjà validé.</param>
    /// <returns>Les noms entre accolades.</returns>
    public static IReadOnlyList<string> Placeholders(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        List<string> names = [];
        int at = 0;

        while (at < path.Length)
        {
            int open = path.IndexOf('{', at);

            if (open < 0)
            {
                break;
            }

            int close = path.IndexOf('}', open);

            if (close < 0)
            {
                break;
            }

            names.Add(path[(open + 1)..close]);
            at = close + 1;
        }

        return names;
    }

    /// <summary>Vérifie les accolades : appairées, non imbriquées, non vides, non répétées.</summary>
    private static Result<string>? Braces(string path)
    {
        int depth = 0;
        int start = -1;
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int at = 0; at < path.Length; at++)
        {
            if (path[at] == '{')
            {
                if (++depth > 1)
                {
                    return Refuse("un paramètre de chemin ne s'imbrique pas dans un autre");
                }

                start = at;

                continue;
            }

            if (path[at] != '}')
            {
                continue;
            }

            if (--depth < 0)
            {
                return Refuse("une accolade fermante n'a pas d'ouvrante");
            }

            string name = path[(start + 1)..at];

            if (name.Length == 0)
            {
                return Refuse("un paramètre de chemin porte un nom");
            }

            if (!seen.Add(name))
            {
                return Refuse("« " + name + " » apparaît deux fois dans ce chemin");
            }
        }

        return depth != 0 ? Refuse("une accolade ouvrante n'est pas fermée") : null;
    }

    private static Result<string> Refuse(string rule) =>
        Result.Fail<string>(FailureReason.InvalidInput, "Chemin refusé : " + rule + ".");
}
