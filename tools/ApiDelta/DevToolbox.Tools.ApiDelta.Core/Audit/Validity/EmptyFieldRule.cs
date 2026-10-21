using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les champs présents et vides.
/// </summary>
/// <remarks>
/// Un champ vide est plus trompeur qu'un champ absent : il se présente comme renseigné. Une documentation
/// externe dont l'URL est une chaîne vide, une étiquette sans nom, un serveur sans adresse — chacun passe
/// la validation structurelle et ne dit rien.
/// </remarks>
public sealed class EmptyFieldRule : AuditRule
{
    private static readonly string[] Collections = ["servers", "tags"];

    /// <summary>Crée la règle.</summary>
    public EmptyFieldRule()
        : base(
            "AUD-010",
            SpecObjectKind.Document,
            SpecObjectKind.Info,
            SpecObjectKind.ExternalDocs,
            SpecObjectKind.Tag,
            SpecObjectKind.Server,
            SpecObjectKind.OAuthFlow)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        List<Finding> findings = [];

        findings.AddRange(Blank(subject, SpecObjectKind.ExternalDocs, "url", "la documentation externe"));
        findings.AddRange(Blank(subject, SpecObjectKind.ExternalDocs, "description", "la documentation externe"));
        findings.AddRange(Blank(subject, SpecObjectKind.Tag, "name", "une étiquette"));
        findings.AddRange(Blank(subject, SpecObjectKind.Tag, "description", "une étiquette"));
        findings.AddRange(Blank(subject, SpecObjectKind.Server, "url", "un serveur"));
        findings.AddRange(Blank(subject, SpecObjectKind.Info, "title", "le bloc d'identité"));
        findings.AddRange(EmptyScopes(subject));
        findings.AddRange(EmptySequences(subject));

        return RuleReport.Evaluated(findings);
    }

    /// <summary>Les champs présents dont la valeur ne dit rien.</summary>
    private IEnumerable<Finding> Blank(
        AuditSubject subject,
        SpecObjectKind kind,
        string field,
        string owner) =>
        subject.Nodes(kind)
            .Select(node => Nodes.Scalar(node.Node, field))
            .OfType<ScalarNode>()
            .Where(scalar => string.IsNullOrWhiteSpace(scalar.Text))
            .Select(scalar => Report(
                $"Le champ « {field} » de {owner} est présent et vide.",
                scalar.Location,
                "Le renseigner, ou le retirer : un champ vide se présente comme renseigné."));

    /// <summary>Les portées d'un flux OAuth déclarées sans explication.</summary>
    private IEnumerable<Finding> EmptyScopes(AuditSubject subject) =>
        subject.Nodes(SpecObjectKind.OAuthFlow)
            .Select(flow => Nodes.Map(flow.Node, "scopes"))
            .OfType<MappingNode>()
            .SelectMany(scopes => scopes.Entries)
            .Where(scope => scope.Value is ScalarNode { } value && string.IsNullOrWhiteSpace(value.Text))
            .Select(scope => Report(
                $"La portée « {scope.Key.Text} » est déclarée sans explication.",
                scope.Value.Location,
                "Dire ce que la portée autorise : c'est ce qu'un consentement affiche."));

    /// <summary>
    /// Les collections déclarées vides, qui ne disent rien de plus que leur absence.
    /// </summary>
    /// <remarks>
    /// <c>security</c> n'en fait pas partie : une exigence de sécurité vide veut dire quelque chose —
    /// que la sécurité est facultative — et c'est une autre règle qui la signale.
    /// </remarks>
    private IEnumerable<Finding> EmptySequences(AuditSubject subject) =>
        subject.Nodes(SpecObjectKind.Document)
            .SelectMany(document => Collections
                .Select(field => (field, Sequence: Nodes.Sequence(document.Node, field)))
                .Where(pair => pair.Sequence is { Items.Count: 0 })
                .Select(pair => Report(
                    $"Le champ « {pair.field} » est déclaré vide.",
                    pair.Sequence!.Location,
                    "Le retirer : une collection vide et une absence ne disent pas la même chose pour "
                        + "les outils, alors qu'elles devraient.")));
}
