using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les codes de réponse quotés de deux façons dans le même document.
/// </summary>
/// <remarks>
/// Un code de réponse est une chaîne pour OpenAPI. Écrit sans guillemets, YAML en fait un entier, et
/// certains lecteurs l'acceptent quand d'autres le refusent : la description se met alors à dépendre de
/// l'outil qui la lit. Mêler les deux écritures dans un même fichier est la façon la plus sûre de ne pas
/// s'en apercevoir.
/// <para>
/// Ce constat n'existe que parce que l'arbre garde le style d'écriture de chaque scalaire. Un modèle
/// sémantique a déjà perdu la différence au moment où on la lui demande.
/// </para>
/// </remarks>
public sealed class ResponseKeyQuotingRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public ResponseKeyQuotingRule()
        : base("AUD-202", SpecObjectKind.Responses)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<ScalarNode> keys =
        [
            .. subject.Nodes(SpecObjectKind.Responses)

                // Les réponses d'opération seulement : sous « components », les clés sont des noms de
                // composants, et les compter comme des codes de réponse nus inventerait un écart.
                .Where(node => !Nodes.IsUnderComponents(node.Location))
                .Select(node => node.Mapping)
                .OfType<MappingNode>()
                .SelectMany(responses => responses.Entries)
                .Select(entry => entry.Key)
                .Where(key => !string.Equals(key.Text, "default", StringComparison.Ordinal)),
        ];

        IReadOnlyList<ScalarNode> bare = [.. keys.Where(key => key.Style == ScalarStyle.Plain)];
        IReadOnlyList<ScalarNode> quoted = [.. keys.Where(key => key.Style != ScalarStyle.Plain)];

        if (bare.Count == 0 || quoted.Count == 0)
        {
            return RuleReport.Clean;
        }

        return RuleReport.Evaluated(
        [
            Report(
                $"Les codes de réponse sont quotés {quoted.Count} fois et laissés nus {bare.Count} fois.",
                bare[0].Location,
                "Les quoter partout : un code de réponse est une chaîne, et nu, YAML en fait un entier.") with
            {
                Locations = [.. bare.Select(key => key.Location)],
                Evidence = new FindingEvidence(Occurrences: bare.Count),
            },
        ]);
    }
}
