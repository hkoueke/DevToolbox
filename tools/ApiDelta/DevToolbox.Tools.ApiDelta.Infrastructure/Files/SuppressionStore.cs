using System.Globalization;
using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Yaml;
using YamlDotNet.Core;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Lit un fichier de sourdines, et refuse celui qu'il ne comprend pas.
/// </summary>
/// <remarks>
/// La lecture ne juge pas du contenu : elle rend ce que le fichier porte, et c'est le validateur qui dit
/// si une sourdine est acceptable. La séparation compte, parce que les deux questions ont des réponses
/// différentes — « ce fichier est-il du YAML bien formé » et « cette décision est-elle motivée » — et
/// qu'un lecteur doit savoir laquelle des deux lui est reprochée.
/// </remarks>
public sealed class SuppressionStore : ISuppressionStore
{
    /// <summary>Le nom par convention, cherché à côté de la description.</summary>
    public const string ConventionalName = "apidelta.suppressions.yaml";

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "ruleId",
        "location",
        "justification",
        "owner",
        "expires",
    };

    /// <inheritdoc />
    public async Task<SuppressionReadResult> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[] content;

        try
        {
            content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SuppressionReadResult.Refused(
                [Invalid(path, "ce fichier n'a pas pu être lu : " + exception.Message)]);
        }

        List<Diagnostic> found = [];

        if (Parse(path, content, found) is not MappingNode root)
        {
            return SuppressionReadResult.Refused(found);
        }

        if (root.Find("suppressions") is not SequenceNode entries)
        {
            found.Add(Invalid(path, "ce fichier porte une suite sous la clé « suppressions »."));

            return SuppressionReadResult.Refused(found);
        }

        List<Suppression> suppressions = [.. entries.Items
            .Select(item => One(path, item, found))
            .OfType<Suppression>()];

        return found.Exists(diagnostic => diagnostic.IsBlocking)
            ? SuppressionReadResult.Refused(found)
            : SuppressionReadResult.Read(suppressions);
    }

    /// <inheritdoc />
    public string? Discover(string descriptionPath) =>
        Conventions.Beside(descriptionPath, ConventionalName);

    private static SpecNode? Parse(string path, byte[] content, List<Diagnostic> found)
    {
        try
        {
            string decoded = Encoding.UTF8.GetString(content);

            using StringReader text = new(decoded);

            YamlTreeBuilder builder = new(new Parser(text), LoadLimits.Default, new ByteOffsets(decoded));
            SpecNode? root = builder.Build();

            found.AddRange(builder.Diagnostics);

            if (root is not MappingNode)
            {
                found.Add(Invalid(path, "un fichier de sourdines est une association de clés."));
            }

            return root;
        }
        catch (YamlException exception)
        {
            found.Add(Invalid(path, "ce n'est pas du YAML analysable : " + exception.Message));

            return null;
        }
    }

    /// <summary>Une sourdine, ou rien quand l'entrée n'en décrit pas une.</summary>
    private static Suppression? One(string path, SpecNode node, List<Diagnostic> found)
    {
        if (node is not MappingNode mapping)
        {
            found.Add(Invalid(path, "chaque sourdine est une association de clés.", node.Position));

            return null;
        }

        foreach (ScalarNode key in mapping.Entries
            .Where(entry => !KnownKeys.Contains(entry.Key.Text))
            .Select(entry => entry.Key))
        {
            found.Add(Invalid(
                path,
                "« " + key.Text + " » n'est pas un champ de sourdine. Les champs connus sont : "
                + string.Join(", ", KnownKeys.OrderBy(name => name, StringComparer.Ordinal)) + ".",
                key.Position));
        }

        if (Text(mapping, "ruleId") is not { } ruleId)
        {
            found.Add(Invalid(path, "une sourdine sans « ruleId » ne vise rien.", node.Position));

            return null;
        }

        return new Suppression(
            ruleId,
            Text(mapping, "location") ?? string.Empty,
            Text(mapping, "justification"),
            Text(mapping, "owner"),
            Expiry(path, mapping, found));
    }

    /// <summary>
    /// L'échéance, quand il y en a une et qu'elle est une date.
    /// </summary>
    /// <remarks>
    /// Une échéance illisible est une erreur et non une absence d'échéance : la traiter comme « pas
    /// d'échéance » rendrait une sourdine perpétuelle par faute de frappe, ce qui est exactement
    /// l'inverse de ce que son auteur avait écrit.
    /// </remarks>
    private static DateOnly? Expiry(string path, MappingNode mapping, List<Diagnostic> found)
    {
        if (Text(mapping, "expires") is not { } text)
        {
            return null;
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, out DateOnly expires))
        {
            return expires;
        }

        found.Add(Invalid(
            path,
            "« expires » vaut une date de la forme 2027-03-31 ; « " + text + " » n'en est pas une.",
            mapping.Find("expires")?.Position));

        return null;
    }

    private static string? Text(MappingNode mapping, string key) =>
        mapping.Find(key) is ScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Text)
            ? scalar.Text
            : null;

    private static Diagnostic Invalid(string path, string message, SourcePosition? position = null) =>
        new(
            ConfigurationCodes.Invalid,
            DiagnosticSeverity.Error,
            "Dans « " + path + " » : " + message,
            SpecPointer.Root,
            position);
}
