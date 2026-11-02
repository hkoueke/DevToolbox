using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Projection.Rules;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Retrouve les schémas des champs sans modifier les composants de la projection.</summary>
internal sealed class ProjectedInput(MappingNode operation, SpecNode document)
{
    internal MappingNode? RequestBody => Resolve(operation.Find("requestBody"), []);

    internal MappingNode? Content => RequestBody?.Find("content") as MappingNode;

    internal SpecNode Field(FieldPlacement placement)
    {
        if (Content is { } content)
        {
            foreach (MappingEntry media in content.Entries)
            {
                if (media.Value is MappingNode body && Find(body.Find("schema"), placement.Field, []) is { } found)
                {
                    if (placement.Location is not ParameterLocation.Body
                        && Resolve(found, [])?.Find("type") is ScalarNode type && type.Text is "object" or "array")
                    {
                        throw new InvalidOperationException("Le champ « " + placement.Field + " » n'est pas un scalaire ; placez-le dans le corps.");
                    }

                    return found;
                }
            }
        }

        // Historical records can contain a builtin declaration without a projected property.
        // A qualified application type must never be guessed from its local name.
        bool builtin = !placement.Declared.Contains(':', StringComparison.Ordinal)
            || placement.Declared.StartsWith("http://www.w3.org/2001/XMLSchema:", StringComparison.Ordinal)
            || placement.Declared.StartsWith("xs:", StringComparison.Ordinal)
            || placement.Declared.StartsWith("xsd:", StringComparison.Ordinal);
        if (builtin && SimpleTypeFormatRule.Lookup(placement.Declared) is { } mapped)
        {
            List<MappingEntry> entries = [new(TreeEdits.Key("type"), TreeEdits.Key(mapped.Type))];
            if (mapped.Format is { } format)
            {
                entries.Add(new MappingEntry(TreeEdits.Key("format"), TreeEdits.Key(format)));
            }

            return TreeEdits.Mapping(SpecPointer.Root, entries);
        }

        throw new InvalidOperationException("Le schéma du champ « " + placement.Field + " » est introuvable ; la route doit être recomposée.");
    }

    internal MappingNode BodySchema(IReadOnlyList<FieldPlacement> fields)
    {
        MappingNode? original = Content is { Entries.Count: > 0 } content && content.Entries[0].Value is MappingNode media
            ? Resolve(media.Find("schema"), []) : null;
        if (original is not null && Compositions.Any(keyword => original.Find(keyword) is SequenceNode))
        {
            return ComposedBody(original, fields);
        }

        List<MappingEntry> properties = [];
        List<SpecNode> required = [];
        foreach (FieldPlacement field in fields)
        {
            properties.Add(new MappingEntry(TreeEdits.Key(field.Effective), Field(field)));
            if (field.Required)
            {
                required.Add(TreeEdits.Key(field.Effective));
            }
        }

        List<MappingEntry> schema =
        [
            new(TreeEdits.Key("type"), TreeEdits.Key("object")),
            new(TreeEdits.Key("properties"), TreeEdits.Mapping(SpecPointer.Root, properties)),
        ];
        if (original is not null)
        {
            schema.AddRange(original.Entries.Where(entry => entry.Key.Text is "xml" or "nullable" or "additionalProperties"));
        }

        if (required.Count > 0)
        {
            schema.Add(new MappingEntry(TreeEdits.Key("required"), TreeEdits.Sequence(SpecPointer.Root, required)));
        }

        return TreeEdits.Mapping(SpecPointer.Root, schema);
    }

    private MappingNode ComposedBody(MappingNode original, IReadOnlyList<FieldPlacement> fields)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        PropertyNames(original, names, []);
        if (!names.SetEquals(fields.Select(field => field.Field))
            || fields.Any(field => !string.Equals(field.Field, field.Effective, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Le corps utilise une composition (oneOf, anyOf ou allOf). "
                + "Ses champs doivent rester ensemble dans le corps, sans déplacement, exclusion ou renommage ; "
                + "ces choix feraient perdre les contraintes du contrat.");
        }

        List<MappingEntry> entries = [.. original.Entries.Where(entry => !string.Equals(entry.Key.Text, "required", StringComparison.Ordinal))];
        SpecNode[] required = [.. fields.Where(field => field.Required).Select(field => TreeEdits.Key(field.Effective))];
        if (required.Length > 0)
        {
            entries.Add(new MappingEntry(TreeEdits.Key("required"), TreeEdits.Sequence(SpecPointer.Root, required)));
        }

        return TreeEdits.Mapping(SpecPointer.Root, entries);
    }

    private void PropertyNames(SpecNode schema, HashSet<string> names, HashSet<string> seen)
    {
        if (Resolve(schema, seen) is not { } resolved)
        {
            return;
        }

        if (resolved.Find("properties") is MappingNode properties)
        {
            names.UnionWith(properties.Entries.Select(entry => entry.Key.Text));
        }

        foreach (string keyword in Compositions)
        {
            if (resolved.Find(keyword) is SequenceNode branches)
            {
                foreach (SpecNode branch in branches.Items)
                {
                    PropertyNames(branch, names, new HashSet<string>(seen, StringComparer.Ordinal));
                }
            }
        }
    }

    private SpecNode? Find(SpecNode? schema, string field, HashSet<string> seen)
    {
        MappingNode? resolved = Resolve(schema, seen);
        if (resolved?.Find("properties") is MappingNode properties && properties.Find(field) is { } found)
        {
            return found;
        }

        foreach (string composition in Compositions)
        {
            if (resolved?.Find(composition) is not SequenceNode branches)
            {
                continue;
            }

            List<SpecNode> matches = [];
            foreach (SpecNode branch in branches.Items)
            {
                if (Find(branch, field, new HashSet<string>(seen, StringComparer.Ordinal)) is { } match)
                {
                    matches.Add(match);
                }
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                return TreeEdits.Mapping(SpecPointer.Root,
                    [new MappingEntry(TreeEdits.Key(composition), TreeEdits.Sequence(SpecPointer.Root, matches))]);
            }
        }

        return null;
    }

    private MappingNode? Resolve(SpecNode? schema, HashSet<string> seen)
    {
        while (schema is MappingNode mapping)
        {
            if (mapping.Find("$ref") is not ScalarNode reference)
            {
                return mapping;
            }

            if (!reference.Text.StartsWith("#/", StringComparison.Ordinal) || !seen.Add(reference.Text) || seen.Count > 64)
            {
                return null;
            }

            schema = PointerNavigator.Find(document, reference.Text[1..]);
        }

        return null;
    }

    private static readonly string[] Compositions = ["allOf", "oneOf", "anyOf"];
}
