using System.Text.RegularExpressions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Governance;

/// <summary>
/// Rapporte les valeurs qui ont la forme d'un secret, sans jamais en recopier une.
/// </summary>
/// <remarks>
/// Le constat nomme l'emplacement et le motif reconnu, jamais la valeur. C'est la seule façon d'avoir à
/// la fois un rapport utile et un rapport qu'on peut joindre à un ticket : dire « un jeton porteur se
/// trouve en /paths/~1documents/get/parameters/0/example » suffit à aller le chercher, là où le recopier
/// ferait du rapport une deuxième fuite.
/// <para>
/// Les motifs sont volontairement grossiers. Un faux positif coûte une ligne qu'un relecteur écarte ; un
/// faux négatif laisse un identifiant dans un fichier qui circule. La reconnaissance est donc large, et
/// la gravité reste un avertissement plutôt qu'une erreur — l'outil signale une ressemblance, il n'établit
/// pas qu'une chaîne est un secret en service.
/// </para>
/// </remarks>
public sealed class SecretShapedValueRule : AuditRule
{
    /// <summary>Les champs dont la valeur est une donnée d'exemple, et non une contrainte.</summary>
    private static readonly string[] ValueFields = ["example", "default"];

    private static readonly (string Name, Regex Pattern)[] Shapes =
    [
        ("un jeton porteur", Compiled(@"(?i)\bbearer\s+[A-Za-z0-9._\-]{16,}")),
        ("un jeton JWT", Compiled(@"\beyJ[A-Za-z0-9._\-]{12,}")),
        ("un identifiant dans une URL", Compiled(@"[a-z][a-z0-9+.\-]*://[^/\s:@]+:[^/\s@]+@")),
        ("une clé d'interface", Compiled(@"(?i)\b(?:api[_\-]?key|secret|password|motdepasse)\b\s*[:=]\s*\S{8,}")),
        ("une clé privée", Compiled(@"-----BEGIN [A-Z ]*PRIVATE KEY-----")),
    ];

    /// <summary>Crée la règle.</summary>
    public SecretShapedValueRule()
        : base("AUD-306", SpecObjectKind.Server, SpecObjectKind.Parameter, SpecObjectKind.Schema,
            SpecObjectKind.Example, SpecObjectKind.Header, SpecObjectKind.MediaType)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        List<Finding> findings = [];

        foreach (AuditNode node in Visits.Distinct().SelectMany(subject.Nodes))
        {
            findings.AddRange(Suspect(node));
        }

        return RuleReport.Evaluated(
        [
            .. findings
                .GroupBy(finding => finding.Location.Json, StringComparer.Ordinal)
                .Select(group => group.First()),
        ]);
    }

    /// <summary>Les valeurs suspectes qu'un nœud porte directement.</summary>
    private IEnumerable<Finding> Suspect(AuditNode node)
    {
        if (node.Mapping is not { } mapping)
        {
            yield break;
        }

        foreach (MappingEntry entry in mapping.Entries)
        {
            if (!Watched(entry.Key.Text))
            {
                continue;
            }

            foreach (ScalarNode scalar in Scalars(entry.Value))
            {
                if (Shape(scalar.Text) is { } shape)
                {
                    yield return Report(
                        "Cette valeur a la forme de " + shape + ".",
                        scalar.Location,
                        "Retirer la valeur de la description, et la remplacer par un exemple qui "
                        + "n'engage rien.");
                }
            }
        }
    }

    /// <summary>
    /// Indique si un champ porte une donnée plutôt qu'une contrainte.
    /// </summary>
    /// <remarks>
    /// <c>url</c> y figure pour les serveurs, où un identifiant se glisse dans l'autorité. Un champ comme
    /// <c>type</c> ou <c>format</c> n'y figure pas : ce qu'il porte est un mot du vocabulaire d'OpenAPI,
    /// et le passer au crible ne produirait que du bruit.
    /// </remarks>
    private static bool Watched(string key) =>
        ValueFields.Contains(key, StringComparer.Ordinal)
        || string.Equals(key, "url", StringComparison.Ordinal)
        || string.Equals(key, "examples", StringComparison.Ordinal);

    /// <summary>Les scalaires d'un sous-arbre, un exemple pouvant être une structure entière.</summary>
    private static IEnumerable<ScalarNode> Scalars(SpecNode node)
    {
        Stack<SpecNode> pending = new();
        pending.Push(node);

        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case ScalarNode scalar:
                    yield return scalar;
                    break;

                case MappingNode mapping:
                    foreach (MappingEntry entry in mapping.Entries)
                    {
                        pending.Push(entry.Value);
                    }

                    break;

                case SequenceNode sequence:
                    foreach (SpecNode item in sequence.Items)
                    {
                        pending.Push(item);
                    }

                    break;
            }
        }
    }

    /// <summary>Le motif reconnu, s'il y en a un.</summary>
    private static string? Shape(string text) =>
        text.Length < 8
            ? null
            : Shapes.FirstOrDefault(shape => shape.Pattern.IsMatch(text)).Name;

    /// <summary>
    /// Un motif compilé, borné en durée.
    /// </summary>
    /// <remarks>
    /// Borné parce qu'une expression régulière lâchée sur un exemple de deux mégaoctets est une panne
    /// qu'on ne diagnostique pas. Le délai dépassé laisse la valeur non signalée, ce qui est le bon
    /// défaut : la règle est facultative, la lecture ne l'est pas.
    /// </remarks>
    private static Regex Compiled(string pattern) =>
        new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
