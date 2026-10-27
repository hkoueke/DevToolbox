using System.Globalization;
using System.Text;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>
/// Dérive un itinéraire REST d'un nom d'opération, de façon déterministe, et dit comment.
/// </summary>
/// <remarks>
/// Déterministe, et annoncé pour chaque opération. Le développeur doit pouvoir contredire la dérivation —
/// c'est tout l'objet de la correspondance — et il ne peut le faire que s'il voit <em>sur quoi</em> elle
/// s'est fondée.
/// <para>
/// La table de verbes se configure ; le repli, non. Un service de forme RPC n'a aucune forme REST à
/// trouver, et en inventer une serait décider de ce que l'outil ne peut pas savoir. Le repli est donc
/// annoncé comme un repli, et jamais comme une dérivation : les deux mots ne promettent pas la même chose.
/// </para>
/// </remarks>
public static class RouteDerivation
{
    /// <summary>Le séparateur d'un chemin REST. Nommé parce qu'il n'est pas un chemin de fichier.</summary>
    private const string Separator = "/";

    /// <summary>La table de verbes par défaut, en français et en anglais.</summary>
    /// <remarks>
    /// Les deux langues, parce que les contrats de service d'une même maison mélangent couramment les
    /// deux.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> DefaultVerbs { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lire"] = "GET",
            ["obtenir"] = "GET",
            ["consulter"] = "GET",
            ["rechercher"] = "GET",
            ["lister"] = "GET",
            ["interroger"] = "GET",
            ["get"] = "GET",
            ["find"] = "GET",
            ["list"] = "GET",
            ["search"] = "GET",
            ["query"] = "GET",
            ["deposer"] = "POST",
            ["creer"] = "POST",
            ["ajouter"] = "POST",
            ["enregistrer"] = "POST",
            ["create"] = "POST",
            ["add"] = "POST",
            ["submit"] = "POST",
            ["modifier"] = "PUT",
            ["remplacer"] = "PUT",
            ["mettre"] = "PUT",
            ["update"] = "PUT",
            ["replace"] = "PUT",
            ["set"] = "PUT",
            ["corriger"] = "PATCH",
            ["patch"] = "PATCH",
            ["supprimer"] = "DELETE",
            ["effacer"] = "DELETE",
            ["retirer"] = "DELETE",
            ["archiver"] = "DELETE",
            ["delete"] = "DELETE",
            ["remove"] = "DELETE",
        };

    /// <summary>Dérive l'itinéraire d'une opération.</summary>
    /// <param name="operation">Son nom.</param>
    /// <param name="verbs">La table de verbes, ou celle par défaut si rien n'est donné.</param>
    /// <returns>L'itinéraire, et la façon dont il a été obtenu.</returns>
    public static DerivedRoute Derive(
        string operation, IReadOnlyDictionary<string, string>? verbs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        List<string> words = Words(operation);

        if (words.Count < 2)
        {
            // Un seul mot : aucun sujet à distinguer du verbe. C'est un repli, même si ce mot unique se
            // trouve être un verbe connu — « lister » tout court ne dit pas quoi lister.
            return Fallback(operation);
        }

        IReadOnlyDictionary<string, string> table = verbs ?? DefaultVerbs;
        string subject = Plural(string.Concat(words.Skip(1)));

        return table.TryGetValue(words[0], out string? method)
            ? new DerivedRoute(method, Separator + subject, RouteDerivationKind.Derived, words[0])
            : new DerivedRoute("POST", Separator + subject, RouteDerivationKind.Partial, null);
    }

    /// <summary>Le repli : une opération dont aucun verbe ne se dégage.</summary>
    private static DerivedRoute Fallback(string operation) =>
        new("POST", Separator + Lower(operation), RouteDerivationKind.Fallback, null);

    /// <summary>
    /// Découpe un nom d'opération en mots.
    /// </summary>
    /// <remarks>
    /// Sur la casse chameau, les tirets et les soulignés — les trois conventions qu'on rencontre, souvent
    /// dans le même contrat. Une suite de majuscules compte pour un mot, afin que « lireURLDocument »
    /// donne « lire », « URL », « Document » et non une lettre par mot.
    /// </remarks>
    private static List<string> Words(string operation)
    {
        List<string> words = [];
        StringBuilder current = new();

        foreach (char character in operation)
        {
            if (character is '-' or '_' or '.' or ' ')
            {
                Flush(words, current);

                continue;
            }

            if (char.IsUpper(character) && current.Length > 0 && !char.IsUpper(current[^1]))
            {
                Flush(words, current);
            }

            _ = current.Append(character);
        }

        Flush(words, current);

        return words;
    }

    private static void Flush(List<string> words, StringBuilder current)
    {
        if (current.Length > 0)
        {
            words.Add(current.ToString());
            _ = current.Clear();
        }
    }

    /// <summary>
    /// Met un sujet au pluriel, en minuscules.
    /// </summary>
    /// <remarks>
    /// Une règle volontairement grossière : le « s » du français et de l'anglais, sauf si le mot y finit
    /// déjà. Une vraie pluralisation demanderait un dictionnaire par langue, se tromperait tout de même,
    /// et se tromperait de façon <em>imprévisible</em> — ce qui est pire ici, puisque la dérivation doit
    /// être déterministe et que le développeur la corrige en un geste quand elle lui déplaît.
    /// </remarks>
    private static string Plural(string subject)
    {
        string lower = Lower(subject);

        return lower.EndsWith('s') ? lower : lower + "s";
    }

    private static string Lower(string word) => word.ToLower(CultureInfo.InvariantCulture);
}
