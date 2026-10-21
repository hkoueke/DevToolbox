using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les couples type et format qui ne vont pas ensemble.
/// </summary>
/// <remarks>
/// Un format sans rapport avec son type est ignoré par les outils : <c>type: integer</c> avec
/// <c>format: date-time</c> décrit un entier, et personne ne le voit. La table ci-dessous ne retient que
/// les formats dont OpenAPI nomme le type ; un format inconnu est laissé tranquille, puisqu'OpenAPI
/// autorise explicitement à en inventer.
/// </remarks>
public sealed class TypeFormatRule : AuditRule
{
    private static readonly Dictionary<string, string> Owner = new(StringComparer.Ordinal)
    {
        ["int32"] = "integer",
        ["int64"] = "integer",
        ["float"] = "number",
        ["double"] = "number",
        ["byte"] = "string",
        ["binary"] = "string",
        ["date"] = "string",
        ["date-time"] = "string",
        ["password"] = "string",
        ["email"] = "string",
        ["uuid"] = "string",
        ["uri"] = "string",
        ["hostname"] = "string",
        ["ipv4"] = "string",
        ["ipv6"] = "string",
    };

    /// <summary>Crée la règle.</summary>
    public TypeFormatRule()
        : base("AUD-009", SpecObjectKind.Schema)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. subject.Nodes(SpecObjectKind.Schema)
                .Select(schema => (schema, Mismatch: Mismatch(schema.Node)))
                .Where(pair => pair.Mismatch is not null)
                .Select(pair => Report(
                    pair.Mismatch!,
                    pair.schema.Location,
                    "Accorder le type et le format, ou retirer le format.")),
        ]);
    }

    /// <summary>Ce qui ne va pas, quand quelque chose ne va pas.</summary>
    private static string? Mismatch(SpecNode schema)
    {
        if (Nodes.Text(schema, "format") is not { } format
            || !Owner.TryGetValue(format, out string? expected))
        {
            return null;
        }

        string? declared = Declared(schema);

        if (declared is null)
        {
            return $"Le format « {format} » est déclaré sans type ; il suppose « {expected} ».";
        }

        return string.Equals(declared, expected, StringComparison.Ordinal)
            ? null
            : $"Le format « {format} » appartient au type « {expected} », pas à « {declared} ».";
    }

    /// <summary>Le type déclaré, qu'il soit écrit seul ou en union.</summary>
    private static string? Declared(SpecNode schema)
    {
        if (Nodes.Text(schema, "type") is { } single)
        {
            return single;
        }

        return Nodes.Sequence(schema, "type")?.Items
            .OfType<ScalarNode>()
            .Select(item => item.Text)
            .FirstOrDefault(text => !string.Equals(text, "null", StringComparison.Ordinal));
    }
}
