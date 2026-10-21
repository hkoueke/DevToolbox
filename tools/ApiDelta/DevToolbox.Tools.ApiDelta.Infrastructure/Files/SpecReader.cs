using System.Security.Cryptography;
using System.Text;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Json;
using DevToolbox.Tools.ApiDelta.Infrastructure.Internal;
using DevToolbox.Tools.ApiDelta.Infrastructure.Yaml;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Lit une description en un arbre fidèle, ou explique pourquoi elle ne l'est pas.
/// </summary>
/// <remarks>
/// L'ordre compte : la forme est détectée d'après le contenu, l'arbre est construit avec ses clés en
/// double relevées, et c'est seulement si tout cela a tenu que la version est lue et qu'un document
/// existe. Un document à moitié lu n'est jamais rendu — il n'y a pas de chemin pour le construire.
/// </remarks>
public sealed class SpecReader : ISpecReader
{
    private readonly ILogger<SpecReader> _logger;

    /// <summary>Crée le lecteur.</summary>
    /// <param name="logger">Reçoit l'issue de chaque lecture.</param>
    public SpecReader(ILogger<SpecReader> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SpecReadResult> ReadAsync(
        string path,
        DocumentRole role,
        LoadLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(limits);

        byte[] content;

        try
        {
            content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SpecReadResult.Failed(
            [
                new Diagnostic(
                    DiagnosticCodes.ParseFailed,
                    DiagnosticSeverity.Error,
                    "Ce fichier n'a pas pu être lu : " + exception.Message),
            ]);
        }

        if (content.Length > limits.MaxBytes)
        {
            return SpecReadResult.Failed(
            [
                new Diagnostic(
                    DiagnosticCodes.LimitReached,
                    DiagnosticSeverity.Error,
                    $"Ce fichier pèse {content.Length} octets, au-delà de la limite de {limits.MaxBytes}."),
            ]);
        }

        return Read(path, role, limits, content);
    }

    /// <inheritdoc />
    public SpecReadResult ReadBytes(
        ReadOnlyMemory<byte> content,
        string name,
        DocumentRole role,
        LoadLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(limits);

        if (content.Length > limits.MaxBytes)
        {
            return SpecReadResult.Failed(
            [
                new Diagnostic(
                    DiagnosticCodes.LimitReached,
                    DiagnosticSeverity.Error,
                    $"Ce contenu pèse {content.Length} octets, au-delà de la limite de {limits.MaxBytes}."),
            ]);
        }

        return Read(name, role, limits, content.ToArray());
    }

    private SpecReadResult Read(string path, DocumentRole role, LoadLimits limits, byte[] content)
    {
        SpecFormat format = FormatDetector.Detect(content);

        List<Diagnostic> diagnostics = [];
        SpecNode? root = TryBuild(format, content, limits, diagnostics);

        if (root is null || diagnostics.Exists(diagnostic => diagnostic.IsBlocking))
        {
            Log.ReadRefused(_logger, path, diagnostics.Count);

            return SpecReadResult.Failed(diagnostics);
        }

        return Complete(path, role, format, content, root, diagnostics);
    }

    /// <summary>
    /// Construit l'arbre, en convertissant toute sortie par exception en diagnostic situé.
    /// </summary>
    private static SpecNode? TryBuild(
        SpecFormat format,
        byte[] content,
        LoadLimits limits,
        List<Diagnostic> diagnostics)
    {
        try
        {
            return format == SpecFormat.Json
                ? BuildJson(content, limits, diagnostics)
                : BuildYaml(content, limits, diagnostics);
        }
        catch (LoadLimitReachedException exception)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.LimitReached,
                DiagnosticSeverity.Error,
                exception.Message,
                exception.Location,
                exception.Position));
        }
        catch (YamlException exception)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "Ce fichier n'est pas du YAML analysable : " + exception.Message,
                SpecPointer.Root,
                new SourcePosition((int)exception.Start.Line, (int)exception.Start.Column)));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            // L'analyseur YAML ne lève pas toujours une YamlException sur une entrée malformée : certains
            // états le font sortir par une exception générique, sans position. L'entrée étant par nature
            // hostile, elle doit devenir un refus et non une panne — faute de quoi un fichier mal écrit
            // ferait tomber l'outil au lieu d'être rejeté.
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "Ce fichier n'est pas du YAML analysable : " + exception.Message,
                SpecPointer.Root));
        }
        catch (System.Text.Json.JsonException exception)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "Ce fichier n'est pas du JSON analysable : " + exception.Message,
                SpecPointer.Root,
                new SourcePosition(
                    (int)(exception.LineNumber + 1 ?? 1), (int)(exception.BytePositionInLine + 1 ?? 1))));
        }

        return null;
    }

    /// <summary>
    /// Établit la version déclarée, et seulement alors construit le document.
    /// </summary>
    private SpecReadResult Complete(
        string path,
        DocumentRole role,
        SpecFormat format,
        byte[] content,
        SpecNode root,
        List<Diagnostic> diagnostics)
    {
        if (!VersionDetector.TryDetect(root, out OpenApiVersion version, out Diagnostic? refusal))
        {
            diagnostics.Add(refusal!);
            Log.ReadRefused(_logger, path, diagnostics.Count);

            return SpecReadResult.Failed(diagnostics);
        }

        diagnostics.AddRange(AmbiguityScanner.Scan(root));

        string hash = Convert.ToHexStringLower(SHA256.HashData(content));

        Log.ReadSucceeded(_logger, path, format, version, diagnostics.Count);

        return new SpecReadResult(
            new SpecDocument(role, path, format, version, content.Length, hash, root, content),
            diagnostics);
    }

    private static SpecNode BuildYaml(byte[] content, LoadLimits limits, List<Diagnostic> diagnostics)
    {
        string decoded = Encoding.UTF8.GetString(content);

        using StringReader text = new(decoded);

        YamlTreeBuilder builder = new(new Parser(text), limits, new ByteOffsets(decoded));
        SpecNode? root = builder.Build();

        diagnostics.AddRange(builder.Diagnostics);

        return root ?? throw new YamlException("Ce fichier ne contient aucun document YAML.");
    }

    private static SpecNode BuildJson(byte[] content, LoadLimits limits, List<Diagnostic> diagnostics)
    {
        JsonTreeBuilder builder = new(new LineIndex(content), limits);
        SpecNode root = builder.Build(content);

        diagnostics.AddRange(builder.Diagnostics);

        return root;
    }
}
