using System.Text.RegularExpressions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Governance;

/// <summary>
/// Rapporte les substituants d'environnement absents, mal écrits ou inconnus dans les serveurs.
/// </summary>
/// <remarks>
/// Une URL de serveur qui code en dur un environnement est la façon la plus courante de publier une
/// description de recette en production. Un substituant mal écrit est pire : la chaîne de déploiement ne
/// le remplace pas, et l'URL publiée contient le gabarit.
/// <para>
/// Le motif et la liste des substituants connus viennent du jeu de règles. Sans eux, la règle n'a aucun
/// avis — un outil qui déciderait de la forme des substituants d'une maison se tromperait partout
/// ailleurs.
/// </para>
/// </remarks>
public sealed class PlaceholderRule : AuditRule
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    /// <summary>Crée la règle.</summary>
    public PlaceholderRule()
        : base("AUD-303", SpecObjectKind.Server)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (string.IsNullOrEmpty(subject.Ruleset.EnvironmentPlaceholderPattern))
        {
            return RuleReport.NotEvaluated(
                "Aucun motif de substituant n'est configuré : la règle n'a rien à vérifier.");
        }

        Regex pattern = new(
            subject.Ruleset.EnvironmentPlaceholderPattern,
            RegexOptions.CultureInvariant,
            Timeout);

        List<Finding> findings = [];

        foreach (AuditNode server in subject.Nodes(SpecObjectKind.Server))
        {
            if (Nodes.Scalar(server.Node, "url") is not { } url || url.Text.Length == 0)
            {
                continue;
            }

            findings.AddRange(Inspect(subject, pattern, url));
        }

        return RuleReport.Evaluated(findings);
    }

    /// <summary>Ce qu'une URL de serveur a à se reprocher.</summary>
    private IEnumerable<Finding> Inspect(AuditSubject subject, Regex pattern, ScalarNode url)
    {
        IReadOnlyList<string> found =
        [
            .. pattern.Matches(url.Text)
                .Select(match => match.Groups["name"].Success
                    ? match.Groups["name"].Value
                    : match.Value),
        ];

        if (found.Count == 0)
        {
            // L'URL n'est pas recopiée, et c'est délibéré : une adresse de serveur porte parfois un
            // identifiant dans son autorité, et un rapport qui la citerait ferait du constat une seconde
            // fuite. L'emplacement suffit à aller la lire — c'est à cela qu'il sert.
            yield return Report(
                "Cette URL de serveur ne porte aucun substituant d'environnement.",
                url.Location,
                "Y placer le substituant attendu, pour que la même description serve dans chaque "
                    + "environnement.",
                FindingSeverity.Warning);

            yield break;
        }

        foreach (string name in found
            .Where(name => !subject.Ruleset.KnownPlaceholders.Contains(name, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            yield return Report(
                $"Le substituant « {name} » de l'URL de serveur n'est pas connu.",
                url.Location,
                "Le déclarer parmi les substituants connus, ou corriger son nom : "
                    + "un substituant inconnu n'est pas remplacé.");
        }
    }
}
