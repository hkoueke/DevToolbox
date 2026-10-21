using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les noms qui s'écartent des conventions que l'équipe s'est données.
/// </summary>
/// <remarks>
/// La règle ne tient aucune convention pour évidente. Tant que rien n'est déclaré, elle se taît : un outil
/// qui imposerait son goût produirait des centaines de constats dès la première exécution, et c'est ainsi
/// qu'un audit devient quelque chose qu'on n'ouvre plus.
/// </remarks>
public sealed class NamingConventionRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public NamingConventionRule()
        : base(
            "AUD-204",
            SpecObjectKind.PathItem,
            SpecObjectKind.Operation,
            SpecObjectKind.Components,
            SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        NamingPolicy policy = subject.Ruleset.Naming;

        return RuleReport.Evaluated(
        [
            .. Segments(subject, policy.PathSegments),
            .. OperationIds(subject, policy.OperationIds),
            .. Components(subject, policy.Components),
            .. Properties(subject, policy.Properties),
        ]);
    }

    /// <summary>Les segments de chemin, hors substituants.</summary>
    private IEnumerable<Finding> Segments(AuditSubject subject, NamingStyle style) =>
        subject.Nodes(SpecObjectKind.PathItem)
            .SelectMany(item => Nodes.Name(item.Location)
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !segment.StartsWith('{'))
                .Where(segment => !Matches(segment, style))
                .Select(segment => Deviation(segment, "segment de chemin", style, item.Location)));

    private IEnumerable<Finding> OperationIds(AuditSubject subject, NamingStyle style) =>
        subject.Nodes(SpecObjectKind.Operation)
            .Select(operation => (operation, Id: Nodes.Text(operation.Node, "operationId")))
            .Where(pair => pair.Id is not null && !Matches(pair.Id, style))
            .Select(pair => Deviation(
                pair.Id!, "identifiant d'opération", style, pair.operation.Location));

    private IEnumerable<Finding> Components(AuditSubject subject, NamingStyle style) =>
        subject.Nodes(SpecObjectKind.Components)
            .Select(node => node.Mapping)
            .OfType<MappingNode>()
            .SelectMany(components => components.Entries)
            .Select(family => family.Value)
            .OfType<MappingNode>()
            .SelectMany(members => members.Entries)
            .Where(member => !Matches(member.Key.Text, style))
            .Select(member => Deviation(
                member.Key.Text, "nom de composant", style, member.Value.Location));

    private IEnumerable<Finding> Properties(AuditSubject subject, NamingStyle style) =>
        subject.Nodes(SpecObjectKind.Schema)
            .Select(schema => Nodes.Map(schema.Node, "properties"))
            .OfType<MappingNode>()
            .SelectMany(properties => properties.Entries)
            .Where(property => !Matches(property.Key.Text, style))
            .Select(property => Deviation(
                property.Key.Text, "nom de propriété", style, property.Value.Location));

    private Finding Deviation(string name, string what, NamingStyle style, SpecPointer location) =>
        Report(
            $"Le {what} « {name} » ne suit pas la convention {Named(style)}.",
            location,
            $"Le renommer, ou revoir la convention. Renommer change ce que les clients engendrés voient : "
                + "ce n'est jamais un remède automatique.");

    private static string Named(NamingStyle style) => style switch
    {
        NamingStyle.Camel => "« camel »",
        NamingStyle.Pascal => "« pascal »",
        NamingStyle.Kebab => "« kebab »",
        NamingStyle.Snake => "« snake »",
        _ => "convenue",
    };

    /// <summary>Indique si un nom suit une forme.</summary>
    private static bool Matches(string? name, NamingStyle style)
    {
        if (style == NamingStyle.Any || string.IsNullOrEmpty(name))
        {
            return true;
        }

        return style switch
        {
            NamingStyle.Camel => char.IsAsciiLetterLower(name[0]) && name.All(char.IsAsciiLetterOrDigit),
            NamingStyle.Pascal => char.IsAsciiLetterUpper(name[0]) && name.All(char.IsAsciiLetterOrDigit),
            NamingStyle.Kebab => Separated(name, '-'),
            NamingStyle.Snake => Separated(name, '_'),
            _ => true,
        };
    }

    /// <summary>Indique si un nom est fait de mots minuscules séparés par un caractère donné.</summary>
    private static bool Separated(string name, char separator) =>
        name.Length > 0
        && name[0] != separator
        && name[^1] != separator
        && !name.Contains(string.Concat(separator, separator), StringComparison.Ordinal)
        && name.All(character =>
            character == separator || char.IsAsciiDigit(character) || char.IsAsciiLetterLower(character));
}
