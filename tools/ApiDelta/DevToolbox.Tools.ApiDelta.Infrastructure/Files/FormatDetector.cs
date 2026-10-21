using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Détermine la forme d'une description d'après son contenu.
/// </summary>
/// <remarks>
/// D'après le contenu, et non d'après l'extension : un fichier nommé <c>.yaml</c> qui contient du JSON est
/// du JSON, et le lire autrement reviendrait à se tromper sur ce que l'auteur a écrit. Le signe est le
/// premier caractère utile : une description OpenAPI est une association, donc un document JSON commence
/// par une accolade.
/// </remarks>
internal static class FormatDetector
{
    internal static SpecFormat Detect(ReadOnlySpan<byte> content)
    {
        foreach (byte current in content)
        {
            switch (current)
            {
                case (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n':
                    continue;

                // Nomenclature d'octets : sautée, puis la recherche se poursuit.
                case 0xEF or 0xBB or 0xBF:
                    continue;

                case (byte)'{' or (byte)'[':
                    return SpecFormat.Json;

                default:
                    return SpecFormat.Yaml;
            }
        }

        return SpecFormat.Yaml;
    }
}
