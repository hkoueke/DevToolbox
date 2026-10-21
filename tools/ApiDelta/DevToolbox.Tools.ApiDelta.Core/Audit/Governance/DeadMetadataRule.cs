using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Governance;

/// <summary>
/// Rapporte les métadonnées déclarées que rien n'emploie : étiquettes et mécanismes de sécurité.
/// </summary>
/// <remarks>
/// Une étiquette qu'aucune opération ne porte apparaît tout de même dans un portail, vide. Un mécanisme de
/// sécurité qu'aucune exigence ne nomme donne à lire une authentification qui n'est demandée nulle part.
/// Les deux sont inoffensifs et trompeurs, ce qui est la définition d'une métadonnée morte.
/// <para>
/// Les composants inaccessibles ne sont pas signalés ici : c'est une autre règle, et la dire deux fois
/// doublerait le travail du relecteur sans rien lui apprendre.
/// </para>
/// </remarks>
public sealed class DeadMetadataRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public DeadMetadataRule()
        : base("AUD-305", SpecObjectKind.Document, SpecObjectKind.Tag, SpecObjectKind.SecurityScheme)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated([.. Tags(subject), .. Schemes(subject)]);
    }

    /// <summary>Les étiquettes déclarées qu'aucune opération ne porte.</summary>
    private IEnumerable<Finding> Tags(AuditSubject subject)
    {
        HashSet<string> used = new(
            subject.Nodes(SpecObjectKind.Operation)
                .SelectMany(operation => Nodes.Items(operation.Node, "tags"))
                .OfType<ScalarNode>()
                .Select(tag => tag.Text),
            StringComparer.Ordinal);

        return subject.Nodes(SpecObjectKind.Tag)
            .Select(tag => (tag, Name: Nodes.Text(tag.Node, "name")))
            .Where(pair => pair.Name is not null && !used.Contains(pair.Name))
            .Select(pair => Report(
                $"L'étiquette « {pair.Name} » n'est portée par aucune opération.",
                pair.tag.Location,
                "La retirer, ou l'employer."));
    }

    /// <summary>Les mécanismes de sécurité qu'aucune exigence ne nomme.</summary>
    private IEnumerable<Finding> Schemes(AuditSubject subject)
    {
        HashSet<string> named = new(
            Requirements(subject).SelectMany(requirement => requirement.Entries
                .Select(entry => entry.Key.Text)),
            StringComparer.Ordinal);

        return subject.Nodes(SpecObjectKind.SecurityScheme)
            .Where(scheme => Nodes.IsComponent(scheme.Location, "securitySchemes"))
            .Where(scheme => !named.Contains(Nodes.Name(scheme.Location)))
            .Select(scheme => Report(
                $"Le mécanisme de sécurité « {Nodes.Name(scheme.Location)} » n'est nommé par aucune "
                    + "exigence.",
                scheme.Location,
                "Le retirer, ou l'exiger quelque part."));
    }

    /// <summary>Les exigences de sécurité du document et de ses opérations.</summary>
    private static IEnumerable<MappingNode> Requirements(AuditSubject subject) =>
        Nodes.Items(subject.Root, "security")
            .Concat(subject.Nodes(SpecObjectKind.Operation)
                .SelectMany(operation => Nodes.Items(operation.Node, "security")))
            .OfType<MappingNode>();
}
