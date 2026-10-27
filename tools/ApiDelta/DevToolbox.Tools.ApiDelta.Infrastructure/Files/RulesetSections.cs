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
    /// La lecture et la projection d'un contrat de service : budget, imports, table de verbes.
    /// </summary>
    /// <remarks>
    /// Trois sous-sections, chacune refusant ce qu'elle ne comprend pas en le nommant. Une clé inconnue
    /// ne tombe pas dans un repli silencieux : un réglage qu'on croit avoir posé et que l'outil n'a
    /// jamais lu est pire qu'un réglage absent, parce qu'on cherche ailleurs la raison du résultat.
    /// </remarks>
    internal static Ruleset Wsdl(Reader reader, SpecNode node)
    {
        reader.Unknown(node, "wsdl", "limits", "imports", "verbs");

        WsdlSettings settings = reader.Ruleset.Wsdl;

        if (Child(node, "limits") is { } limits)
        {
            settings = settings with { Limits = Budget(reader, limits, settings.Limits) };
        }

        if (Child(node, "imports") is { } imports)
        {
            settings = settings with { Imports = Resolution(reader, imports, settings.Imports) };
        }

        if (Child(node, "verbs") is MappingNode verbs)
        {
            settings = settings with { Verbs = Verbs(reader, verbs) };
        }

        return reader.Ruleset with { Wsdl = settings };
    }

    /// <summary>Le budget de lecture d'un contrat.</summary>
    private static WsdlLimits Budget(Reader reader, SpecNode node, WsdlLimits limits)
    {
        reader.Unknown(
            node,
            "wsdl.limits",
            "maxDocumentBytes",
            "maxNestingDepth",
            "maxImportChainDepth",
            "maxDocumentCount",
            "maxEnumerationValues",
            "maxResponseBytes",
            "fetchTimeoutSeconds");

        return limits with
        {
            MaxDocumentBytes = reader.Number(
                node, "wsdl.limits", "maxDocumentBytes", limits.MaxDocumentBytes),
            MaxNestingDepth = (int)reader.Number(
                node, "wsdl.limits", "maxNestingDepth", limits.MaxNestingDepth),
            MaxImportChainDepth = (int)reader.Number(
                node, "wsdl.limits", "maxImportChainDepth", limits.MaxImportChainDepth),
            MaxDocumentCount = (int)reader.Number(
                node, "wsdl.limits", "maxDocumentCount", limits.MaxDocumentCount),
            MaxEnumerationValues = (int)reader.Number(
                node, "wsdl.limits", "maxEnumerationValues", limits.MaxEnumerationValues),
            MaxResponseBytes = reader.Number(
                node, "wsdl.limits", "maxResponseBytes", limits.MaxResponseBytes),
            FetchTimeoutSeconds = (int)reader.Number(
                node, "wsdl.limits", "fetchTimeoutSeconds", limits.FetchTimeoutSeconds),
        };
    }

    /// <summary>
    /// Ce que la résolution des imports s'autorise. Le distant sans hôte est une configuration invalide.
    /// </summary>
    /// <remarks>
    /// Refusé, et non lu comme « tous les hôtes ». Permettre le distant sans nommer personne ne veut
    /// rien dire, et la lecture charitable — celle qui devine ce que l'auteur a voulu — est précisément
    /// celle qui ouvrirait le réseau entier sur une ligne incomplète.
    /// </remarks>
    private static ImportPolicy Resolution(Reader reader, SpecNode node, ImportPolicy policy)
    {
        reader.Unknown(
            node,
            "wsdl.imports",
            "root",
            "allowRemote",
            "allowedHosts",
            "allowPlainHttp",
            "maxRedirects",
            "confirmedAddresses");

        ImportPolicy resolved = policy with
        {
            ConfinementRoot = Reader.Text(node, "root") ?? policy.ConfinementRoot,
            AllowRemote = reader.Flag(node, "wsdl.imports", "allowRemote", policy.AllowRemote),
            AllowedHosts = Reader.Strings(node, "allowedHosts") is { Count: > 0 } hosts
                ? hosts
                : policy.AllowedHosts,
            AllowPlainHttp = reader.Flag(
                node, "wsdl.imports", "allowPlainHttp", policy.AllowPlainHttp),
            MaxRedirects = (int)reader.Number(
                node, "wsdl.imports", "maxRedirects", policy.MaxRedirects),
            ConfirmedAddresses = Reader.Strings(node, "confirmedAddresses") is { Count: > 0 } confirmed
                ? confirmed
                : policy.ConfirmedAddresses,
        };

        if (!resolved.IsCoherent)
        {
            reader.Refuse(
                "« wsdl.imports.allowRemote » demande une liste « allowedHosts » non vide : résoudre "
                + "par le réseau sans liste d'autorisation n'est pas une porte ouverte, c'est une "
                + "erreur.",
                node.Position);
        }

        return resolved;
    }

    /// <summary>
    /// La table de verbes : un mot du nom d'une opération, et la méthode HTTP qu'il commande.
    /// </summary>
    /// <remarks>
    /// Les méthodes admises sont celles qu'une route peut porter. Un verbe associé à autre chose est
    /// refusé en le nommant plutôt qu'ignoré : une table dont la moitié des lignes n'aurait rien fait
    /// produirait des itinéraires que personne n'a demandés, sans que rien ne le dise.
    /// </remarks>
    private static Dictionary<string, string> Verbs(Reader reader, MappingNode node)
    {
        Dictionary<string, string> table = new(StringComparer.OrdinalIgnoreCase);

        foreach (MappingEntry entry in node.Entries)
        {
            if (entry.Value is not ScalarNode method)
            {
                reader.Refuse(
                    "« wsdl.verbs." + entry.Key.Text + " » vaut une méthode HTTP.", entry.Key.Position);

                continue;
            }

            string wanted = method.Text.ToUpperInvariant();

            if (!Methods.Contains(wanted))
            {
                reader.Refuse(
                    "« wsdl.verbs." + entry.Key.Text + " » vaut " + string.Join(", ", Methods) + ".",
                    method.Position);

                continue;
            }

            table[entry.Key.Text] = wanted;
        }

        return table;
    }

    private static SpecNode? Child(SpecNode node, string key) => (node as MappingNode)?.Find(key);

    /// <summary>Les méthodes qu'un itinéraire dérivé peut porter.</summary>
    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE",
    };

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

        /// <summary>
        /// Refuse, une par une, les clés qu'une section ne connaît pas.
        /// </summary>
        /// <param name="node">La section.</param>
        /// <param name="section">Son nom, pour que le refus dise où chercher.</param>
        /// <param name="known">Les clés admises.</param>
        /// <remarks>
        /// Nommée avec sa ligne et sa colonne, parce qu'une faute de frappe dans une configuration est
        /// invisible autrement : le réglage qu'on croit avoir posé n'agit pas, l'outil se comporte comme
        /// s'il n'existait pas, et on cherche la raison du résultat partout sauf dans le fichier.
        /// </remarks>
        internal void Unknown(SpecNode node, string section, params string[] known)
        {
            if (node is not MappingNode mapping)
            {
                Refuse("« " + section + " » est une association.", node.Position);

                return;
            }

            HashSet<string> admitted = new(known, StringComparer.Ordinal);

            foreach (ScalarNode key in mapping.Entries
                .Select(entry => entry.Key)
                .Where(key => !admitted.Contains(key.Text)))
            {
                Refuse(
                    "« " + section + "." + key.Text + " » n'est pas une clé connue. Celles de "
                        + "cette section sont : " + string.Join(", ", known) + ".",
                    key.Position);
            }
        }

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
