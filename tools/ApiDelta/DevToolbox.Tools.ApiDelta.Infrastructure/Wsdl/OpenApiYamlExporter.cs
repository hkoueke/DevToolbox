using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Émet un document OpenAPI projeté en YAML UTF-8.</summary>
/// <remarks>
/// Réutilise l'émetteur canonique, qui conserve les types scalaires et échappe les
/// références JSON Pointer. Ne modifie pas le document source.
/// </remarks>
public sealed class OpenApiYamlExporter
{
    /// <summary>Écrit le brouillon OpenAPI en YAML.</summary>
    /// <param name="document">Le document OpenAPI construit.</param>
    /// <param name="formatting">Les options de formatage, facultatives.</param>
    /// <returns>Les octets YAML UTF-8.</returns>
    public ReadOnlyMemory<byte> Export(
        SchemaDraft document, CanonicalFormatting? formatting = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        SpecNode root = DraftWriter.Write(document, SpecPointer.Root);

        return new CanonicalEmitter().Emit(root, formatting ?? CanonicalFormatting.Default);
    }
}
