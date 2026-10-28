using System.Globalization;
using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Yaml;
using YamlDotNet.Core;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Lit un jeu de règles, et refuse tout ce qu'il ne comprend pas.
/// </summary>
/// <remarks>
/// Le fichier est lu par le même constructeur d'arbre que les descriptions, et non par une
/// désérialisation vers un type annoté. Deux raisons, et la seconde est la plus importante : chaque
/// diagnostic porte une ligne et une colonne, et une clé inconnue peut être <em>nommée</em> plutôt que
/// silencieusement ignorée. Une désérialisation tolérante est précisément ce qui produit un fichier de
/// configuration dont une section mal orthographiée ne fait rien pendant un an.
/// <para>
/// Un identifiant de règle inexistant est une erreur, pas un avertissement. Une faute de frappe qui
/// n'éteint rien est la façon dont une équipe en vient à croire qu'une règle est éteinte alors qu'elle
/// est allumée — et l'inverse.
/// </para>
/// </remarks>
public sealed class RulesetStore : IRulesetStore
{
    /// <summary>Le nom par convention, cherché à côté de la description.</summary>
    public const string ConventionalName = "apidelta.ruleset.yaml";

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "rules",
        "protectedExtensions",
        "keepComponents",
        "deprecationPhrases",
        "standardHeaders",
        "standardResponses",
        "suppressions",
        "output",
        "failOn",
        "limits",
        "references",
        "canonicalFormatting",
        "versioning",
        "namingConventions",
        "environmentPlaceholders",
        "wsdl",
    };

    private static readonly HashSet<string> KnownRuleKeys = new(StringComparer.Ordinal)
    {
        "enabled",
        "severity",
        "minimumOccurrences",
    };

    /// <inheritdoc />
    public async Task<RulesetReadResult> ReadAsync(
        string path,
        Ruleset defaults,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(defaults);

        byte[] content;

        try
        {
            content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return RulesetReadResult.Refused(
                [Invalid(path, "ce fichier n'a pas pu être lu : " + exception.Message)]);
        }

        List<Diagnostic> found = [];

        if (Parse(path, content, found) is not MappingNode root)
        {
            return RulesetReadResult.Refused(found);
        }

        Ruleset ruleset = Apply(path, root, defaults, found);

        return found.Exists(diagnostic => diagnostic.IsBlocking)
            ? RulesetReadResult.Refused(found)
            : RulesetReadResult.Read(ruleset);
    }

    /// <inheritdoc />
    public string? Discover(string descriptionPath) =>
        Conventions.Beside(descriptionPath, ConventionalName);

    /// <summary>Construit l'arbre du fichier, ou dit pourquoi il n'en est pas un.</summary>
    private static SpecNode? Parse(string path, byte[] content, List<Diagnostic> found)
    {
        try
        {
            // Un jeu de règles enregistré sous Windows porte lui aussi une nomenclature d'octets, et
            // sa première clé était lue comme « FEFFrules » : le fichier était refusé en nommant une
            // clé inconnue que son auteur voyait pourtant écrite correctement.
            int preamble = Preamble.Length(content);
            string decoded = Preamble.Trim(Encoding.UTF8.GetString(content));

            using StringReader text = new(decoded);

            YamlTreeBuilder builder =
                new(new Parser(text), LoadLimits.Default, new ByteOffsets(decoded, preamble));
            SpecNode? root = builder.Build();

            found.AddRange(builder.Diagnostics);

            if (root is not MappingNode)
            {
                found.Add(Invalid(path, "un jeu de règles est une association de clés."));
            }

            return root;
        }
        catch (YamlException exception)
        {
            found.Add(Invalid(path, "ce n'est pas du YAML analysable : " + exception.Message));

            return null;
        }
    }

    /// <summary>Superpose le fichier aux valeurs livrées, clé par clé.</summary>
    private static Ruleset Apply(
        string path,
        MappingNode root,
        Ruleset defaults,
        List<Diagnostic> found)
    {
        Ruleset ruleset = defaults;

        foreach (MappingEntry entry in root.Entries)
        {
            if (!KnownKeys.Contains(entry.Key.Text))
            {
                found.Add(Invalid(
                    path,
                    "la clé « " + entry.Key.Text + " » n'est pas reconnue. Les clés connues sont : "
                    + string.Join(", ", KnownKeys.OrderBy(key => key, StringComparer.Ordinal)) + ".",
                    entry.Key.Position));

                continue;
            }

            ruleset = Entry(path, entry, ruleset, found);
        }

        return ruleset;
    }

    private static Ruleset Entry(
        string path,
        MappingEntry entry,
        Ruleset ruleset,
        List<Diagnostic> found) => entry.Key.Text switch
        {
            "rules" => ruleset with { Rules = Rules(path, entry.Value, found) },

            // Les préfixes protégés ne font qu'ajouter de la protection : ceux du fichier rejoignent
            // ceux qui sont livrés, et aucun jeu de règles ne peut en retirer un.
            "protectedExtensions" => ruleset with
            {
                ProtectedExtensionPrefixes =
                    [.. ruleset.ProtectedExtensionPrefixes.Concat(Strings(entry.Value)).Distinct(
                        StringComparer.Ordinal)],
            },

            "keepComponents" => ruleset with { KeepComponentPatterns = Strings(entry.Value) },
            "deprecationPhrases" => ruleset with { DeprecationPhrases = Strings(entry.Value) },
            "standardHeaders" => ruleset with { StandardHeaders = Strings(entry.Value) },
            "standardResponses" => ruleset with { StandardResponses = Strings(entry.Value) },
            "suppressions" => Suppressions(path, entry.Value, ruleset, found),
            "output" => Output(path, entry.Value, ruleset, found),
            "failOn" => Threshold(path, entry.Value, ruleset, found),
            _ => Section(path, entry, ruleset, found),
        };

    /// <summary>Les sections structurées, chacune refusant ce qu'elle ne comprend pas.</summary>
    private static Ruleset Section(
        string path,
        MappingEntry entry,
        Ruleset ruleset,
        List<Diagnostic> found)
    {
        RulesetSections.Reader reader = new(ruleset, path, found);

        return entry.Key.Text switch
        {
            "limits" => RulesetSections.Limits(reader, entry.Value),
            "references" => RulesetSections.References(reader, entry.Value),
            "canonicalFormatting" => RulesetSections.Formatting(reader, entry.Value),
            "versioning" => RulesetSections.Versioning(reader, entry.Value),
            "namingConventions" => RulesetSections.Naming(reader, entry.Value),
            "environmentPlaceholders" => RulesetSections.Placeholders(reader, entry.Value),
            "wsdl" => RulesetSections.Wsdl(reader, entry.Value),

            // Inatteignable : une clé inconnue a déjà été nommée, avec sa ligne et sa colonne, par le
            // contrôle qui précède. Cette branche servait autrefois de repli vers la section
            // d'extraction, si bien qu'une faute de frappe sur un nom de section se lisait comme un
            // réglage ; elle ne lit plus rien.
            _ => reader.Ruleset,
        };
    }

    /// <summary>Les réglages par règle, un identifiant inconnu étant une erreur.</summary>
    private static Dictionary<string, RuleSetting> Rules(
        string path,
        SpecNode node,
        List<Diagnostic> found)
    {
        Dictionary<string, RuleSetting> rules = new(StringComparer.Ordinal);

        if (node is not MappingNode mapping)
        {
            found.Add(Invalid(path, "« rules » est une association d'identifiants à des réglages."));

            return rules;
        }

        foreach (MappingEntry entry in mapping.Entries)
        {
            if (!Known(entry.Key.Text))
            {
                found.Add(Invalid(
                    path,
                    "« " + entry.Key.Text + " » n'est pas une règle connue.",
                    entry.Key.Position));

                continue;
            }

            rules[entry.Key.Text] = Setting(path, entry.Key.Text, entry.Value, found);
        }

        return rules;
    }

    /// <summary>Indique si un identifiant désigne une règle que l'outil porte.</summary>
    private static bool Known(string ruleId) =>
        RuleDefaults.All.ContainsKey(ruleId)
        || (ruleId.StartsWith("OPT-", StringComparison.Ordinal)
            && OptimizationRuleIds.Contains(ruleId));

    /// <summary>Les règles d'optimisation, dont la liste est publiée par le tableau d'OPT.</summary>
    private static readonly HashSet<string> OptimizationRuleIds = new(StringComparer.Ordinal)
    {
        "OPT-001", "OPT-002", "OPT-003", "OPT-004", "OPT-005",
        "OPT-006", "OPT-007", "OPT-008", "OPT-009",
    };

    private static RuleSetting Setting(
        string path,
        string ruleId,
        SpecNode node,
        List<Diagnostic> found)
    {
        RuleSetting setting = RuleSetting.Default;

        if (node is not MappingNode mapping)
        {
            found.Add(Invalid(path, "le réglage de « " + ruleId + " » est une association."));

            return setting;
        }

        foreach (MappingEntry entry in mapping.Entries)
        {
            if (!KnownRuleKeys.Contains(entry.Key.Text))
            {
                found.Add(Invalid(
                    path,
                    "« " + entry.Key.Text + " » n'est pas un réglage de règle. Les réglages connus "
                    + "sont : enabled, severity, minimumOccurrences.",
                    entry.Key.Position));

                continue;
            }

            setting = Field(path, ruleId, entry, setting, found);
        }

        return setting;
    }

    private static RuleSetting Field(
        string path,
        string ruleId,
        MappingEntry entry,
        RuleSetting setting,
        List<Diagnostic> found)
    {
        string? text = (entry.Value as ScalarNode)?.Text;

        switch (entry.Key.Text)
        {
            case "enabled":
                if (!bool.TryParse(text, out bool enabled))
                {
                    found.Add(Invalid(
                        path,
                        "« enabled » de " + ruleId + " vaut « true » ou « false ».",
                        entry.Key.Position));

                    return setting;
                }

                return setting with { Enabled = enabled };

            case "severity":
                if (Severity(text) is not { } severity)
                {
                    found.Add(Invalid(
                        path,
                        "« severity » de " + ruleId + " vaut error, warning, information ou "
                        + "opportunity.",
                        entry.Key.Position));

                    return setting;
                }

                return setting with { Severity = severity };

            default:
                if (!int.TryParse(text, CultureInfo.InvariantCulture, out int minimum) || minimum < 2)
                {
                    found.Add(Invalid(
                        path,
                        "« minimumOccurrences » de " + ruleId + " est un entier d'au moins deux : "
                        + "une seule écriture n'est pas une répétition.",
                        entry.Key.Position));

                    return setting;
                }

                return setting with { MinimumOccurrences = minimum };
        }
    }

    private static FindingSeverity? Severity(string? text) => text switch
    {
        "error" => FindingSeverity.Error,
        "warning" => FindingSeverity.Warning,
        "information" => FindingSeverity.Information,
        "opportunity" => FindingSeverity.Opportunity,
        _ => null,
    };

    private static Ruleset Suppressions(
        string path,
        SpecNode node,
        Ruleset ruleset,
        List<Diagnostic> found)
    {
        if (node is MappingNode mapping
            && mapping.Find("requireJustification") is ScalarNode scalar)
        {
            if (bool.TryParse(scalar.Text, out bool required))
            {
                return ruleset with { RequireSuppressionJustification = required };
            }

            found.Add(Invalid(
                path, "« requireJustification » vaut « true » ou « false ».", scalar.Position));
        }

        return ruleset;
    }

    private static Ruleset Output(
        string path,
        SpecNode node,
        Ruleset ruleset,
        List<Diagnostic> found)
    {
        if (node is MappingNode mapping && mapping.Find("maxGrowthPercent") is ScalarNode scalar)
        {
            if (int.TryParse(scalar.Text, CultureInfo.InvariantCulture, out int percent)
                && percent >= 0)
            {
                return ruleset with { MaxGrowthPercent = percent };
            }

            found.Add(Invalid(
                path, "« maxGrowthPercent » est un entier positif ou nul.", scalar.Position));
        }

        return ruleset;
    }

    private static Ruleset Threshold(
        string path,
        SpecNode node,
        Ruleset ruleset,
        List<Diagnostic> found)
    {
        FailureThreshold? threshold = (node as ScalarNode)?.Text switch
        {
            "never" => FailureThreshold.Never,
            "warning" => FailureThreshold.OnWarning,
            "error" => FailureThreshold.OnError,
            "breakingChange" => FailureThreshold.OnBreakingChange,
            _ => null,
        };

        if (threshold is not { } resolved)
        {
            found.Add(Invalid(
                path,
                "« failOn » vaut never, warning, error ou breakingChange.",
                node.Position));

            return ruleset;
        }

        return ruleset with { Threshold = resolved };
    }

    /// <summary>Une suite de chaînes, chaque élément non scalaire étant ignoré.</summary>
    private static IReadOnlyList<string> Strings(SpecNode node) =>
        node is SequenceNode sequence
            ? [.. sequence.Items.OfType<ScalarNode>().Select(scalar => scalar.Text)]
            : [];

    private static Diagnostic Invalid(string path, string message, SourcePosition? position = null) =>
        new(
            ConfigurationCodes.Invalid,
            DiagnosticSeverity.Error,
            "Dans « " + path + " » : " + message,
            SpecPointer.Root,
            position);
}
