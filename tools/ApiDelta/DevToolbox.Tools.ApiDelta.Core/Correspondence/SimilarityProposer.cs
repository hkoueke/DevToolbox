using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Correspondence;

/// <summary>
/// Propose un appariement par recouvrement de jetons, et renonce devant une quasi-égalité.
/// </summary>
/// <remarks>
/// Le recouvrement de jetons plutôt qu'une distance d'édition, et c'est un choix de lisibilité autant que
/// de justesse : les jetons communs se <em>montrent</em>. Un développeur qui lit « d'après les jetons
/// document, identifiant » sait sur quoi l'outil s'est fondé, et peut contredire en un geste. Une
/// distance de 0,82 ne lui apprendrait rien qu'il puisse discuter.
/// <para>
/// Renoncer devant deux candidats également plausibles est le comportement central de ce type, et non un
/// cas limite. L'état ambigu existe précisément pour que l'incertitude ne soit pas cachée : rendre le
/// premier des deux au hasard de l'ordre de lecture produirait un rapport assuré sur la mauvaise paire,
/// et personne ne pourrait s'en douter.
/// </para>
/// </remarks>
public sealed class SimilarityProposer : ICorrespondenceProposer
{
    /// <summary>L'écart en deçà duquel deux candidats sont tenus pour également plausibles.</summary>
    /// <remarks>
    /// Un quart de point. Assez large pour que « PUT /documents » et « PATCH /documents » soient déclarés
    /// ambigus face à « majDocument » — ce qui est le cas que l'on veut attraper — et assez étroit pour
    /// qu'un appariement franc ne soit pas noyé dans le doute.
    /// </remarks>
    public const double TieMargin = 0.25;

    /// <inheritdoc />
    public OperationCorrespondence Propose(
        IReadOnlyList<string> operations,
        IReadOnlyList<string> routes,
        IReadOnlyDictionary<string, string>? declared = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(routes);

        List<CorrespondenceEntry> entries = [];

        foreach (string operation in operations)
        {
            entries.Add(Entry(operation, routes, declared));
        }

        return new OperationCorrespondence(entries);
    }

    /// <summary>L'entrée d'une opération : déclarée si elle l'est, proposée sinon.</summary>
    /// <remarks>
    /// La déclaration primé sur toute proposition, sans que l'outil la discute : une équipe qui a écrit
    /// ce fichier a tranché. L'outil ne vérifie même pas que la route existe en production — si elle
    /// n'existe pas, la couverture le dira, et ce n'est pas la même nouvelle qu'un appariement refusé.
    /// </remarks>
    private static CorrespondenceEntry Entry(
        string operation, IReadOnlyList<string> routes, IReadOnlyDictionary<string, string>? declared)
    {
        if (declared is not null && declared.TryGetValue(operation, out string? stated))
        {
            return new CorrespondenceEntry(operation, stated, CorrespondenceOrigin.Declared);
        }

        IReadOnlyList<Candidate> ranked = Ranked(operation, routes);

        if (ranked.Count == 0 || ranked[0].Overlap <= 0)
        {
            return new CorrespondenceEntry(operation, string.Empty, CorrespondenceOrigin.Ambiguous);
        }

        IReadOnlyList<Candidate> contenders =
        [
            .. ranked.Where(candidate => ranked[0].Overlap - candidate.Overlap < TieMargin),
        ];

        return contenders.Count > 1
            ? new CorrespondenceEntry(
                operation,
                contenders[0].Route,
                CorrespondenceOrigin.Ambiguous,
                Basis(contenders[0]),
                [.. contenders.Skip(1).Select(candidate => candidate.Route)])
            : new CorrespondenceEntry(
                operation,
                ranked[0].Route,
                CorrespondenceOrigin.Proposed,
                Basis(ranked[0]));
    }

    /// <summary>Les routes classées par recouvrement décroissant, à égalité près de l'ordre donné.</summary>
    /// <remarks>
    /// Le tri est stable sur le nom de la route à score égal, pour que deux exécutions sur les mêmes
    /// entrées rendent la même proposition. Une proposition qui changerait d'une séance à l'autre ferait
    /// qu'une reprise ne porterait plus sur la même chose.
    /// </remarks>
    private static IReadOnlyList<Candidate> Ranked(string operation, IReadOnlyList<string> routes)
    {
        IReadOnlySet<string> wanted = Tokens(operation);

        return
        [
            .. routes
                .Select(route => Score(operation, route, wanted))
                .Where(candidate => candidate.Overlap > 0)
                .OrderByDescending(candidate => candidate.Overlap)
                .ThenBy(candidate => candidate.Route, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Le recouvrement entre une opération et une route.
    /// </summary>
    /// <remarks>
    /// Le score combine deux choses. La part des jetons de l'opération retrouvés dans la route dit si le
    /// sujet concorde ; l'accord de la méthode avec le verbe dérivé du nom dit si l'action concorde. Le
    /// sujet pèse plus, parce qu'une route sur le bon sujet avec la mauvaise méthode reste la bonne
    /// famille, alors que l'inverse ne l'est pas.
    /// <para>
    /// L'accord de méthode ne compte que si un verbe a été <em>reconnu</em>. Une première version prenait
    /// le « POST » de repli pour une déduction, si bien qu'une opération sans verbe reconnu préférait
    /// silencieusement les routes « POST » : l'outil tranchait sur ce qu'il venait d'admettre ne pas
    /// savoir, et une quasi-égalité qui devait rester ambiguë sortait proposée.
    /// </para>
    /// </remarks>
    private static Candidate Score(string operation, string route, IReadOnlySet<string> wanted)
    {
        IReadOnlySet<string> offered = Tokens(Path(route));

        IReadOnlyList<string> shared = [.. wanted.Where(token => Matches(token, offered))];

        if (shared.Count == 0)
        {
            return new Candidate(route, 0, []);
        }

        double subject = (double)shared.Count / wanted.Count;
        DerivedRoute derived = RouteDerivation.Derive(operation);

        double verb = derived.Kind == RouteDerivationKind.Derived
            && string.Equals(Method(route), derived.Method, StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

        return new Candidate(route, (subject * 0.75) + (verb * 0.25), shared);
    }

    /// <summary>
    /// Indique qu'un jeton de l'opération se retrouve parmi ceux de la route.
    /// </summary>
    /// <remarks>
    /// Au singulier près : une route parle de « documents » là où une opération parle de « document », et
    /// exiger l'égalité stricte ferait rater l'appariement le plus courant qui soit.
    /// </remarks>
    private static bool Matches(string token, IReadOnlySet<string> offered) =>
        offered.Contains(token)
        || offered.Contains(token + "s")
        || (token.EndsWith('s') && offered.Contains(token[..^1]));

    /// <summary>Les jetons d'un nom : ses mots, en minuscules, les verbes connus écartés.</summary>
    /// <remarks>
    /// Les verbes sont écartés du <em>sujet</em> parce qu'ils sont déjà pris en compte par la méthode. Les
    /// garder ferait concorder « lireDocument » avec « GET /lire » mieux qu'avec « GET /documents ».
    /// </remarks>
    private static HashSet<string> Tokens(string name)
    {
        HashSet<string> tokens = new(StringComparer.Ordinal);

        foreach (string word in Split(name))
        {
            string lower = word.ToLower(CultureInfo.InvariantCulture);

            if (lower.Length > 1 && !RouteDerivation.DefaultVerbs.ContainsKey(lower))
            {
                _ = tokens.Add(lower);
            }
        }

        return tokens.Count > 0 ? tokens : new HashSet<string>(Split(name), StringComparer.Ordinal);
    }

    /// <summary>Découpe un nom sur la casse chameau, les tirets, les soulignés et les séparateurs.</summary>
    private static IEnumerable<string> Split(string name)
    {
        System.Text.StringBuilder current = new();

        foreach (char character in name)
        {
            if (!char.IsLetterOrDigit(character))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    _ = current.Clear();
                }

                continue;
            }

            if (char.IsUpper(character) && current.Length > 0 && !char.IsUpper(current[^1]))
            {
                yield return current.ToString();
                _ = current.Clear();
            }

            _ = current.Append(character);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static string Method(string route)
    {
        int space = route.IndexOf(' ', StringComparison.Ordinal);

        return space < 0 ? "GET" : route[..space];
    }

    private static string Path(string route)
    {
        int space = route.IndexOf(' ', StringComparison.Ordinal);

        return space < 0 ? route : route[(space + 1)..];
    }

    private static string Basis(Candidate candidate) =>
        "jetons : "
            + string.Join(", ", candidate.SharedTokens.OrderBy(token => token, StringComparer.Ordinal));

    private sealed record Candidate(string Route, double Overlap, IReadOnlyList<string> SharedTokens);
}
