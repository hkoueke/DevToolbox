using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les désaccords entre le gabarit d'un chemin et ses paramètres de chemin.
/// </summary>
/// <remarks>
/// Dans les deux sens : un substituant du gabarit que rien ne déclare, et un paramètre de chemin déclaré
/// qui n'apparaît pas dans le gabarit. Les deux rendent la description inexploitable par un client
/// engendré, et aucun des deux ne se voit à la lecture d'un fichier de cinq mille lignes.
/// <para>
/// Un paramètre qui n'est qu'une référence hors du document ne peut pas être lu. L'élément de chemin est
/// alors laissé de côté et la règle le dit, au lieu d'annoncer un désaccord qu'elle n'a pas constaté.
/// </para>
/// </remarks>
public sealed class PathParameterRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public PathParameterRule()
        : base("AUD-004", SpecObjectKind.PathItem)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        List<Finding> findings = [];
        List<string> unread = [];

        foreach (AuditNode node in subject.Nodes(SpecObjectKind.PathItem))
        {
            string template = Nodes.Name(node.Location);
            HashSet<string> placeholders = Placeholders(template);
            HashSet<string> declared = new(StringComparer.Ordinal);

            if (!Declared(subject.Root, node, declared))
            {
                unread.Add(template);
                continue;
            }

            findings.AddRange(Missing(placeholders, declared, template, node));
            findings.AddRange(Extra(placeholders, declared, template, node));
            findings.AddRange(Optional(subject.Root, node, template));
        }

        return unread.Count == 0
            ? RuleReport.Evaluated(findings)
            : RuleReport.Truncated(
                findings,
                "Les chemins suivants portent un paramètre hors du document et n'ont pas été examinés : "
                    + string.Join(", ", unread));
    }

    /// <summary>Les substituants du gabarit, dans l'ordre où ils apparaissent.</summary>
    private static HashSet<string> Placeholders(string template)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        int cursor = 0;

        while (cursor < template.Length)
        {
            int open = template.IndexOf('{', cursor);

            if (open < 0)
            {
                break;
            }

            int close = template.IndexOf('}', open);

            if (close < 0)
            {
                break;
            }

            names.Add(template[(open + 1)..close]);
            cursor = close + 1;
        }

        return names;
    }

    /// <summary>
    /// Rassemble les paramètres de chemin déclarés, au niveau du chemin et de chaque opération.
    /// </summary>
    /// <returns><see langword="false"/> si l'un d'eux n'a pas pu être lu.</returns>
    private static bool Declared(SpecNode root, AuditNode node, HashSet<string> declared)
    {
        if (node.Mapping is not { } item)
        {
            return true;
        }

        IEnumerable<SpecNode> lists = Nodes.Items(item, "parameters")
            .Concat(item.Entries
                .Where(entry => PathRows.Methods.Contains(entry.Key.Text))
                .SelectMany(entry => Nodes.Items(entry.Value, "parameters")));

        foreach (SpecNode parameter in lists)
        {
            if (Nodes.Resolve(root, parameter) is not { } resolved)
            {
                return false;
            }

            if (string.Equals(Nodes.Text(resolved, "in"), "path", StringComparison.Ordinal)
                && Nodes.Text(resolved, "name") is { } name)
            {
                declared.Add(name);
            }
        }

        return true;
    }

    /// <summary>Les paramètres de chemin que la description ne déclare pas obligatoires.</summary>
    /// <remarks>
    /// Un substituant du gabarit est toujours présent dans l'URL appelée : un paramètre de chemin
    /// facultatif décrit une requête qui ne peut pas exister.
    /// </remarks>
    private IEnumerable<Finding> Optional(SpecNode root, AuditNode node, string template) =>
        PathParameters(root, node)
            .Where(parameter => Nodes.Flag(parameter, "required") != true)
            .Select(parameter => Report(
                $"Le paramètre de chemin « {Nodes.Text(parameter, "name")} » de « {template} » "
                    + "n'est pas déclaré obligatoire.",
                parameter.Location,
                "Déclarer « required: true » : un substituant de gabarit est toujours fourni."));

    /// <summary>Les paramètres de chemin lisibles d'un élément de chemin et de ses opérations.</summary>
    private static IEnumerable<SpecNode> PathParameters(SpecNode root, AuditNode node)
    {
        if (node.Mapping is not { } item)
        {
            return [];
        }

        return Nodes.Items(item, "parameters")
            .Concat(item.Entries
                .Where(entry => PathRows.Methods.Contains(entry.Key.Text))
                .SelectMany(entry => Nodes.Items(entry.Value, "parameters")))
            .Select(parameter => Nodes.Resolve(root, parameter))
            .OfType<SpecNode>()
            .Where(parameter => string.Equals(
                Nodes.Text(parameter, "in"), "path", StringComparison.Ordinal));
    }

    private IEnumerable<Finding> Missing(
        HashSet<string> placeholders,
        HashSet<string> declared,
        string template,
        AuditNode node) =>
        placeholders
            .Where(placeholder => !declared.Contains(placeholder))
            .OrderBy(placeholder => placeholder, StringComparer.Ordinal)
            .Select(placeholder => Report(
                $"Le chemin « {template} » emploie « {{{placeholder}}} » sans déclarer le paramètre correspondant.",
                node.Location,
                $"Déclarer un paramètre « {placeholder} » dans « path », obligatoire."));

    private IEnumerable<Finding> Extra(
        HashSet<string> placeholders,
        HashSet<string> declared,
        string template,
        AuditNode node) =>
        declared
            .Where(name => !placeholders.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => Report(
                $"Le paramètre de chemin « {name} » est déclaré mais n'apparaît pas dans « {template} ».",
                node.Location,
                "Retirer le paramètre, ou corriger le gabarit du chemin."));
}
