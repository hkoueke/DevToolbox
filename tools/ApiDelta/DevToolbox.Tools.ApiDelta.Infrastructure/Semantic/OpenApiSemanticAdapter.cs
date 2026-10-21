using System.Text.Json.Nodes;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;

/// <summary>
/// Valide la structure d'un document en le soumettant à la bibliothèque OpenAPI, puis ramène ses verdicts
/// dans l'arbre.
/// </summary>
/// <remarks>
/// La bibliothèque sert ici, et seulement ici : établir qu'un document respecte la structure de la version
/// qu'il déclare. Elle ne sert jamais à écrire — rien de ce que l'outil publie n'est sérialisé depuis son
/// modèle — ni à comparer. C'est ce partage qui rend la fidélité structurelle : une extension que la
/// bibliothèque ignorerait reste intacte, puisqu'elle ne touche pas à ce qui sera écrit.
/// <para>
/// Ses diagnostics sont situés par pointeur ; ils sont donc reportés sur nos nœuds, afin que chaque erreur
/// atteigne l'écran avec une ligne et une colonne.
/// </para>
/// </remarks>
public sealed class OpenApiSemanticAdapter : ISpecValidator
{
    /// <inheritdoc />
    public IReadOnlyList<Diagnostic> Validate(SpecDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        JsonNode? projected = JsonProjection.Project(document.Root);

        if (projected is null)
        {
            return
            [
                new Diagnostic(
                    DiagnosticCodes.StructuralViolation,
                    DiagnosticSeverity.Error,
                    "Ce document est vide.",
                    document.Root.Location,
                    document.Root.Position),
            ];
        }

        ReadResult result;

        try
        {
            result = OpenApiDocument.Parse(projected.ToJsonString(), "json");
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or ArgumentException or NotSupportedException)
        {
            return
            [
                new Diagnostic(
                    DiagnosticCodes.StructuralViolation,
                    DiagnosticSeverity.Error,
                    "La structure de ce document n'a pas pu être établie : " + exception.Message,
                    document.Root.Location,
                    document.Root.Position),
            ];
        }

        return Translate(result, document);
    }

    /// <summary>Ramène les verdicts de la bibliothèque dans l'arbre.</summary>
    private static List<Diagnostic> Translate(ReadResult result, SpecDocument document)
    {
        List<Diagnostic> diagnostics = [];

        foreach (OpenApiError error in result.Diagnostic?.Errors ?? [])
        {
            diagnostics.Add(Describe(
                error, DiagnosticSeverity.Error, DiagnosticCodes.StructuralViolation, document));
        }

        foreach (OpenApiError warning in result.Diagnostic?.Warnings ?? [])
        {
            diagnostics.Add(Describe(
                warning, DiagnosticSeverity.Warning, DiagnosticCodes.StructuralViolation, document));
        }

        return diagnostics;
    }

    private static Diagnostic Describe(
        OpenApiError error,
        DiagnosticSeverity severity,
        string code,
        SpecDocument document)
    {
        SpecNode? located = PointerNavigator.Find(document.Root, error.Pointer);

        return new Diagnostic(
            code,
            severity,
            error.Message,
            located?.Location ?? document.Root.Location,
            located?.Position ?? document.Root.Position);
    }
}
