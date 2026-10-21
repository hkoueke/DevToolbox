using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>
/// Compare deux descriptions et dit ce qui a changé.
/// </summary>
/// <remarks>
/// La comparaison descend les deux arbres de front, guidée par le métamodèle : lui seul sait qu'une suite
/// de paramètres est un ensemble dont les membres se reconnaissent par nom et emplacement, tandis qu'une
/// composition est une suite dont l'ordre compte.
/// <para>
/// Une différence est signalée une fois, à l'endroit le plus précis qui l'explique. Un parent n'est jamais
/// signalé parce qu'un enfant a changé — sinon un seul mot corrigé dans une description produirait une
/// cascade de « modifié » jusqu'à la racine, et le rapport deviendrait illisible exactement là où il
/// devrait être utile.
/// </para>
/// </remarks>
public sealed class DocumentComparer : IDocumentComparer
{
    private readonly IMetamodel _metamodel;


    /// <summary>Crée le comparateur.</summary>
    /// <param name="metamodel">Dit ce que chaque emplacement est.</param>
    public DocumentComparer(IMetamodel metamodel)
    {
        ArgumentNullException.ThrowIfNull(metamodel);
        _metamodel = metamodel;
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<Difference>> Compare(
        SpecDocument candidate,
        SpecDocument production,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(production);

        List<Difference> differences = [];

        Walk(production.Root, candidate.Root, production.DeclaredVersion, differences, cancellationToken);

        // Tri stable : par emplacement, puis par identifiant de changement. Un rapport dont l'ordre varie
        // d'une exécution à l'autre ne se compare pas, et la comparaison d'artefacts est ce qui prouve le
        // déterminisme.
        return Result.Success<IReadOnlyList<Difference>>(
        [
            .. differences
                .OrderBy(difference => difference.Location.Json, StringComparer.Ordinal)
                .ThenBy(difference => difference.ChangeId, StringComparer.Ordinal),
        ]);
    }

    private void Walk(
        SpecNode before,
        SpecNode after,
        OpenApiVersion version,
        List<Difference> differences,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (before.Kind != after.Kind)
        {
            Report(before.Location, version, DifferenceKind.Modified, before, after, differences);
            return;
        }

        switch (before)
        {
            case MappingNode previous when after is MappingNode current:
                WalkMapping(previous, current, version, differences, cancellationToken);
                break;

            case SequenceNode previous when after is SequenceNode current:
                WalkSequence(previous, current, version, differences, cancellationToken);
                break;

            case ScalarNode previous when after is ScalarNode current:
                if (!string.Equals(
                    previous.Resolved.Canonical, current.Resolved.Canonical, StringComparison.Ordinal))
                {
                    Report(
                        before.Location, version, DifferenceKind.Modified, before, after, differences);
                }

                break;
        }
    }

    private void WalkMapping(
        MappingNode before,
        MappingNode after,
        OpenApiVersion version,
        List<Difference> differences,
        CancellationToken cancellationToken)
    {
        Dictionary<string, SpecNode> current = after.Entries.ToDictionary(
            entry => entry.Key.Text, entry => entry.Value, StringComparer.Ordinal);

        foreach (MappingEntry entry in before.Entries)
        {
            if (current.Remove(entry.Key.Text, out SpecNode? match))
            {
                Walk(entry.Value, match, version, differences, cancellationToken);
                continue;
            }

            Report(
                entry.Value.Location, version, DifferenceKind.Removed, entry.Value, null, differences);
        }

        // Ce qui reste n'existait pas avant : une seule différence par apparition, à sa racine, et non une
        // par nœud du sous-arbre apparu.
        foreach ((string key, SpecNode added) in current)
        {
            Report(
                before.Location.Child(key), version, DifferenceKind.Added, null, added, differences);
        }
    }

    private void WalkSequence(
        SequenceNode before,
        SequenceNode after,
        OpenApiVersion version,
        List<Difference> differences,
        CancellationToken cancellationToken)
    {
        MetamodelEntry entry = _metamodel.Resolve(before.Location, version);

        Dictionary<string, SpecNode> current = Index(after, entry.Identity, version);
        HashSet<string> seen = [];

        for (int index = 0; index < before.Items.Count; index++)
        {
            SpecNode item = before.Items[index];
            string identity = MemberIdentity.Of(item, entry.Identity, index, _metamodel, version);

            if (!seen.Add(identity))
            {
                continue;
            }

            if (current.Remove(identity, out SpecNode? match))
            {
                Walk(item, match, version, differences, cancellationToken);
                continue;
            }

            Report(item.Location, version, DifferenceKind.Removed, item, null, differences);
        }

        foreach ((string _, SpecNode added) in current)
        {
            Report(added.Location, version, DifferenceKind.Added, null, added, differences);
        }
    }

    private Dictionary<string, SpecNode> Index(
        SequenceNode sequence,
        IdentityRule rule,
        OpenApiVersion version)
    {
        Dictionary<string, SpecNode> indexed = new(StringComparer.Ordinal);

        for (int index = 0; index < sequence.Items.Count; index++)
        {
            string identity = MemberIdentity.Of(
                sequence.Items[index], rule, index, _metamodel, version);

            // Un doublon dans un ensemble est une redondance, non une identité supplémentaire : le
            // premier gagne, et l'audit dira que l'autre est en trop.
            indexed.TryAdd(identity, sequence.Items[index]);
        }

        return indexed;
    }

    private void Report(
        SpecPointer location,
        OpenApiVersion version,
        DifferenceKind kind,
        SpecNode? before,
        SpecNode? after,
        List<Difference> differences) =>
        differences.Add(ChangeClassifier.Classify(
            location, _metamodel.Resolve(location, version), kind, before, after));
}
