using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Les sections structurées d'un jeu de règles : limites, références, mise en forme, versionnage,
/// nommage, substituants, extraction.
/// </summary>
/// <remarks>
/// À part du lecteur, pour une raison de lisibilité et une de fond. La lisibilité : sept sections de
/// quatre champs chacune ne tiennent pas dans une méthode. Le fond : chacune refuse ce qu'elle ne
/// comprend pas, et une valeur refusée doit nommer sa section autant que sa clé — « dans
/// <c>limits.maxDepth</c> » se corrige, « valeur invalide » se cherche.
/// </remarks>
internal static class RulesetSections
{
    /// <summary>Les limites de lecture, qui forment le budget contre le déni de service.</summary>
    internal static Ruleset Limits(Reader reader, SpecNode node)
    {
        LoadLimits limits = reader.Ruleset.Limits;

        limits = limits with
        {
            MaxBytes = reader.Number(node, "limits", "maxBytes", limits.MaxBytes),
            MaxDepth = (int)reader.Number(node, "limits", "maxDepth", limits.MaxDepth),
            MaxReferences = (int)reader.Number(node, "limits", "maxReferences", limits.MaxReferences),
            MaxAliasExpansions = (int)reader.Number(
                node, "limits", "maxAliasExpansions", limits.MaxAliasExpansions),
        };

        return reader.Ruleset with
        {
            Limits = limits,
            MaxFindingsPerRule = (int)reader.Number(
                node, "limits", "maxFindingsPerRule", reader.Ruleset.MaxFindingsPerRule),
            MaxNearDuplicateBucket = (int)reader.Number(
                node, "limits", "maxNearDuplicateBucket", reader.Ruleset.MaxNearDuplicateBucket),
        };
    }

    /// <summary>
    /// La politique de référence. Le réseau autorisé sans liste d'hôtes est une configuration invalide.
    /// </summary>
    internal static Ruleset References(Reader reader, SpecNode node)
    {
        ReferencePolicy policy = reader.Ruleset.References with
        {
            AllowLocal = reader.Flag(node, "references", "allowLocal", false),
            AllowRemote = reader.Flag(node, "references", "allowRemote", false),
            RemoteHosts = Reader.Strings(node, "remoteHosts"),
            Root = Reader.Text(node, "root") ?? reader.Ruleset.References.Root,
        };

        if (!policy.IsCoherent)
        {
            reader.Refuse(
                "« references.allowRemote » demande une liste « remoteHosts » non vide : "
                + "résoudre par le réseau sans liste d'autorisation n'est pas une porte ouverte, "
                + "c'est une erreur.",
                node.Position);
        }

        return reader.Ruleset with { References = policy };
    }

    /// <summary>La mise en forme employée quand l'outil écrit une description.</summary>
    internal static Ruleset Formatting(Reader reader, SpecNode node)
    {
        CanonicalFormatting formatting = reader.Ruleset.Formatting;

        return reader.Ruleset with
        {
            Formatting = formatting with
            {
                Indent = (int)reader.Number(node, "canonicalFormatting", "indent", formatting.Indent),
                LineWidth = (int)reader.Number(
                    node, "canonicalFormatting", "lineWidth", formatting.LineWidth),
                LineEndings = reader.Choice(
                    node,
                    "canonicalFormatting",
                    "lineEndings",
                    formatting.LineEndings,
                    ("lf", LineEndingStyle.Lf),
                    ("crlf", LineEndingStyle.Crlf),
                    ("preserve", LineEndingStyle.Preserve)),
                QuoteResponseCodes = reader.Flag(
                    node, "canonicalFormatting", "quoteResponseCodes", formatting.QuoteResponseCodes),
                FinalNewline = reader.Flag(
                    node, "canonicalFormatting", "finalNewline", formatting.FinalNewline),
                KeyOrder = reader.Choice(
                    node,
                    "canonicalFormatting",
                    "keyOrder",
                    formatting.KeyOrder,
                    ("canonical", KeyOrderStyle.Canonical),
                    ("preserve", KeyOrderStyle.Preserve)),
            },
        };
    }

    /// <summary>Ce que valent les changements que la sémantique seule ne tranche pas.</summary>
    internal static Ruleset Versioning(Reader reader, SpecNode node)
    {
        VersioningPolicy policy = reader.Ruleset.Versioning;

        return reader.Ruleset with
        {
            Versioning = policy with
            {
                EnumValueAdded = reader.Impact(
                    node, "enumValueAdded", policy.EnumValueAdded),
                SuccessResponseAdded = reader.Impact(
                    node, "successResponseAdded", policy.SuccessResponseAdded),
                ErrorResponseAdded = reader.Impact(
                    node, "errorResponseAdded", policy.ErrorResponseAdded),
                FallbackBaseline = Reader.Text(node, "fallbackBaseline") ?? policy.FallbackBaseline,
            },
        };
    }

    /// <summary>Les conventions de nommage, dont l'absence est une convention en soi.</summary>
    internal static Ruleset Naming(Reader reader, SpecNode node)
    {
        NamingPolicy policy = reader.Ruleset.Naming;

        return reader.Ruleset with
        {
            Naming = policy with
            {
                PathSegments = reader.Style(node, "paths", policy.PathSegments),
                Components = reader.Style(node, "components", policy.Components),
                OperationIds = reader.Style(node, "operationIds", policy.OperationIds),
                Properties = reader.Style(node, "properties", policy.Properties),
            },
        };
    }

    /// <summary>La forme des substituants d'environnement, et ceux que l'outil connaît.</summary>
    internal static Ruleset Placeholders(Reader reader, SpecNode node) =>
        reader.Ruleset with
        {
            EnvironmentPlaceholderPattern =
                Reader.Text(node, "pattern") ?? reader.Ruleset.EnvironmentPlaceholderPattern,
            KnownPlaceholders = Reader.Strings(node, "known") is { Count: > 0 } known
                ? known
                : reader.Ruleset.KnownPlaceholders,
        };

    /// <summary>Les réglages de l'extraction.</summary>
    internal static Ruleset Extract(Reader reader, SpecNode node)
    {
        ExtractSettings settings = reader.Ruleset.Extract;

        return reader.Ruleset with
        {
            Extract = settings with
            {
                StampProvenance = reader.Flag(
                    node, "extract", "stampProvenance", settings.StampProvenance),
                DefaultName = Reader.Text(node, "defaultName") ?? settings.DefaultName,
                CarryDocumentExtensions = reader.Flag(
                    node, "extract", "carryDocumentExtensions", settings.CarryDocumentExtensions),
            },
        };
    }

    /// <summary>
    /// Ce qu'une section a besoin pour se lire : le jeu de règles en cours, et où déposer un refus.
    /// </summary>
    /// <remarks>
    /// Un porteur mutable pour les diagnostics, et une valeur immuable pour le jeu de règles. La
    /// dissymétrie est voulue : accumuler les refus permet de les rendre tous d'un coup, là où
    /// s'arrêter au premier obligerait à relancer autant de fois qu'il y a de fautes.
    /// </remarks>
    internal sealed class Reader(Ruleset ruleset, string path, List<Diagnostic> diagnostics)
    {
        /// <summary>Le jeu de règles en cours de construction.</summary>
        internal Ruleset Ruleset { get; } = ruleset;

        /// <summary>Consigne un refus, en nommant le fichier.</summary>
        internal void Refuse(string message, SourcePosition? position) =>
            diagnostics.Add(new Diagnostic(
                ConfigurationCodes.Invalid,
                DiagnosticSeverity.Error,
                "Dans « " + path + " » : " + message,
                SpecPointer.Root,
                position));

        /// <summary>Le texte d'une clé, ou rien.</summary>
        internal static string? Text(SpecNode node, string key) =>
            Scalar(node, key) is { } scalar && !string.IsNullOrWhiteSpace(scalar.Text)
                ? scalar.Text
                : null;

        /// <summary>Les chaînes d'une suite, ou une liste vide.</summary>
        internal static IReadOnlyList<string> Strings(SpecNode node, string key) =>
            (node as MappingNode)?.Find(key) is SequenceNode sequence
                ? [.. sequence.Items.OfType<ScalarNode>().Select(item => item.Text)]
                : [];

        /// <summary>Un entier, la valeur en place à défaut, un refus si la valeur n'en est pas un.</summary>
        internal long Number(SpecNode node, string section, string key, long current)
        {
            if (Scalar(node, key) is not { } scalar)
            {
                return current;
            }

            if (long.TryParse(scalar.Text, CultureInfo.InvariantCulture, out long value) && value > 0)
            {
                return value;
            }

            Refuse(
                "« " + section + "." + key + " » est un entier strictement positif.", scalar.Position);

            return current;
        }

        /// <summary>Un booléen, la valeur en place à défaut.</summary>
        internal bool Flag(SpecNode node, string section, string key, bool current)
        {
            if (Scalar(node, key) is not { } scalar)
            {
                return current;
            }

            if (bool.TryParse(scalar.Text, out bool value))
            {
                return value;
            }

            Refuse("« " + section + "." + key + " » vaut « true » ou « false ».", scalar.Position);

            return current;
        }

        /// <summary>Une valeur prise dans une liste fermée, la valeur en place à défaut.</summary>
        internal T Choice<T>(
            SpecNode node,
            string section,
            string key,
            T current,
            params (string Word, T Value)[] words)
        {
            if (Scalar(node, key) is not { } scalar)
            {
                return current;
            }

            foreach ((string word, T value) in words)
            {
                if (string.Equals(scalar.Text, word, StringComparison.Ordinal))
                {
                    return value;
                }
            }

            Refuse(
                "« " + section + "." + key + " » vaut "
                + string.Join(", ", words.Select(pair => pair.Word)) + ".",
                scalar.Position);

            return current;
        }

        /// <summary>Un incrément de version.</summary>
        internal VersionImpact Impact(SpecNode node, string key, VersionImpact current) =>
            Choice(
                node,
                "versioning",
                key,
                current,
                ("major", VersionImpact.Major),
                ("minor", VersionImpact.Minor),
                ("patch", VersionImpact.Patch),
                ("none", VersionImpact.None));

        /// <summary>Une forme de nom.</summary>
        internal NamingStyle Style(SpecNode node, string key, NamingStyle current) =>
            Choice(
                node,
                "namingConventions",
                key,
                current,
                ("any", NamingStyle.Any),
                ("camelCase", NamingStyle.Camel),
                ("PascalCase", NamingStyle.Pascal),
                ("kebab-case", NamingStyle.Kebab),
                ("snake_case", NamingStyle.Snake));

        private static ScalarNode? Scalar(SpecNode node, string key) =>
            (node as MappingNode)?.Find(key) as ScalarNode;
    }
}
