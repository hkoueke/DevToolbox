using System.Text.Json.Nodes;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;

/// <summary>
/// Projette l'arbre de syntaxe en JSON, à l'intention de la couche sémantique.
/// </summary>
/// <remarks>
/// La couche sémantique ne voit ainsi jamais de YAML, et c'est délibéré : la bibliothèque de lecture YAML
/// de l'autre paquet s'appuie sur un second analyseur, et deux analyseurs dans un même processus
/// signifieraient valider ce que l'un a lu tout en comparant ce que l'autre a lu. Toute divergence entre
/// eux — résolution des scalaires, clés en double, alias — deviendrait une classe de bogues où l'audit
/// porte, avec assurance, sur un document qui n'est pas celui qu'on compare.
/// <para>
/// La projection emploie la valeur <em>résolue</em> de chaque scalaire, c'est-à-dire ce que le document
/// dit, et non son écriture.
/// </para>
/// </remarks>
internal static class JsonProjection
{
    internal static JsonNode? Project(SpecNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node switch
        {
            MappingNode mapping => ProjectMapping(mapping),
            SequenceNode sequence => ProjectSequence(sequence),
            ScalarNode scalar => ProjectScalar(scalar),
            _ => null,
        };
    }

    private static JsonObject ProjectMapping(MappingNode mapping)
    {
        JsonObject result = [];

        foreach (MappingEntry entry in mapping.Entries)
        {
            // Une clé en double n'arrive jamais ici : le lecteur refuse le document avant.
            result[entry.Key.Text] = Project(entry.Value);
        }

        return result;
    }

    private static JsonArray ProjectSequence(SequenceNode sequence)
    {
        JsonArray result = [];

        foreach (SpecNode item in sequence.Items)
        {
            result.Add(Project(item));
        }

        return result;
    }

    private static JsonValue? ProjectScalar(ScalarNode scalar) => scalar.Resolved.Kind switch
    {
        ScalarValueKind.Null => null,
        ScalarValueKind.Boolean => JsonValue.Create(
            string.Equals(scalar.Resolved.Canonical, "true", StringComparison.Ordinal)),
        ScalarValueKind.Integral => ProjectIntegral(scalar.Resolved.Canonical),
        ScalarValueKind.Number => ProjectNumber(scalar.Resolved.Canonical),

        // Une écriture ambiguë est projetée comme la chaîne que YAML 1.2 en fait. Le doute a déjà été
        // signalé ; il n'a pas à être tranché deux fois, et sûrement pas autrement ici.
        _ => JsonValue.Create(scalar.Text),
    };

    private static JsonValue ProjectIntegral(string canonical) =>
        long.TryParse(canonical, System.Globalization.CultureInfo.InvariantCulture, out long value)
            ? JsonValue.Create(value)
            : JsonValue.Create(canonical);

    private static JsonValue ProjectNumber(string canonical) =>
        double.TryParse(canonical, System.Globalization.CultureInfo.InvariantCulture, out double value)
            && double.IsFinite(value)
                ? JsonValue.Create(value)

                // Infini et NaN n'existent pas en JSON : la forme textuelle est conservée plutôt que
                // remplacée par une valeur qui mentirait.
                : JsonValue.Create(canonical);
}
