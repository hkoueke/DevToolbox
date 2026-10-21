using System.Globalization;
using System.Reflection;
using System.Text;

namespace DevToolbox.Tests.Tools.ApiDelta.Corpus;

/// <summary>
/// Donne accès au corpus d'essai d'apidelta, embarqué dans l'assemblage de tests.
/// </summary>
internal static class CorpusLoader
{
    private const string Prefix = "DevToolbox.Tests.Tools.ApiDelta.Corpus.";

    private static readonly Assembly Assembly = typeof(CorpusLoader).Assembly;

    /// <summary>Lit un document du corpus tel qu'il est écrit.</summary>
    internal static string Read(string fileName)
    {
        using Stream? stream = Assembly.GetManifestResourceStream(Prefix + fileName)
            ?? throw new InvalidOperationException(
                $"Le document « {fileName} » n'est pas embarqué. Ressources présentes : "
                + string.Join(", ", Names()));

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Lit un document et convertit ses fins de ligne en CRLF.
    /// </summary>
    /// <remarks>
    /// Les fins de ligne ne sont pas conservées dans un dépôt de manière fiable, si bien qu'un fichier
    /// « à fins de ligne Windows » n'en serait pas un partout. La conversion a donc lieu ici : la
    /// comparaison doit rester insensible à ce choix, et c'est ce que le test vérifie.
    /// </remarks>
    internal static string ReadWithCrLf(string fileName) =>
        Read(fileName).ReplaceLineEndings("\r\n");

    /// <summary>Les noms des documents embarqués, pour les messages d'erreur.</summary>
    internal static IEnumerable<string> Names() =>
        Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(name => name[Prefix.Length..])
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// Engendre une description volumineuse : le sujet de l'objectif de tenue en charge.
    /// </summary>
    /// <param name="operations">Le nombre d'opérations.</param>
    /// <param name="schemas">Le nombre de schémas.</param>
    /// <remarks>
    /// Engendrée plutôt que versionnée : le dépôt n'a pas à porter une quinzaine de mégaoctets de YAML
    /// dont la seule vertu est d'être gros.
    /// </remarks>
    internal static string GenerateLarge(int operations = 1000, int schemas = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemas);

        StringBuilder builder = new(operations * 400 + schemas * 120);

        builder.AppendLine("openapi: 3.0.3");
        builder.AppendLine("info:");
        builder.AppendLine("  title: Description engendree");
        builder.AppendLine("  version: 1.0.0");
        builder.AppendLine("paths:");

        for (int i = 0; i < operations; i++)
        {
            AppendGeneratedOperation(builder, i, schemas);
        }

        builder.AppendLine("components:");
        builder.AppendLine("  schemas:");

        for (int i = 0; i < schemas; i++)
        {
            AppendGeneratedSchema(builder, i);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Engendre une description dont l'imbrication dépasse la profondeur autorisée.
    /// </summary>
    /// <param name="depth">Le nombre de niveaux imbriqués.</param>
    /// <remarks>
    /// Engendrée pour la même raison que <see cref="GenerateLarge"/>, en pire : l'indentation croissant
    /// avec la profondeur, quatre cents niveaux pèsent près d'un mégaoctet pour une seule propriété utile.
    /// </remarks>
    internal static string GenerateDeep(int depth = 400)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        StringBuilder builder = new();

        builder.AppendLine("openapi: 3.0.3");
        builder.AppendLine("info:");
        builder.AppendLine("  title: Imbrication excessive");
        builder.AppendLine("  version: 1.0.0");
        builder.AppendLine("paths:");
        builder.AppendLine("  /profond:");
        builder.AppendLine("    get:");
        builder.AppendLine("      operationId: lireProfond");
        builder.AppendLine("      responses:");
        builder.AppendLine("        '200':");
        builder.AppendLine("          description: Une reponse trop imbriquee.");
        builder.AppendLine("          content:");
        builder.AppendLine("            application/json:");
        builder.AppendLine("              schema:");

        // Un seul espace par niveau : légal en YAML, et c'est ce qui garde le document lisible en mémoire.
        for (int level = 0; level < depth; level++)
        {
            string indent = new(' ', 16 + level);
            builder.Append(indent).AppendLine("type: object");
            builder.Append(indent).AppendLine("properties:");
            builder.Append(indent).Append(' ')
                .Append("niveau").Append(level.ToString(CultureInfo.InvariantCulture)).AppendLine(":");
        }

        builder.Append(new string(' ', 16 + depth)).AppendLine("type: string");

        return builder.ToString();
    }

    private static void AppendGeneratedOperation(StringBuilder builder, int index, int schemas)
    {
        string number = index.ToString(CultureInfo.InvariantCulture);

        builder.Append("  /ressource").Append(number).AppendLine(":");
        builder.AppendLine("    get:");
        builder.Append("      operationId: lireRessource").AppendLine(number);
        builder.AppendLine("      parameters:");
        builder.AppendLine("        - name: X-serviceEmetteur");
        builder.AppendLine("          in: header");
        builder.AppendLine("          required: true");
        builder.AppendLine("          schema:");
        builder.AppendLine("            type: string");
        builder.AppendLine("      responses:");
        builder.AppendLine("        '200':");
        builder.AppendLine("          description: La ressource.");
        builder.AppendLine("          content:");
        builder.AppendLine("            application/json:");
        builder.AppendLine("              schema:");
        builder.Append("                $ref: '#/components/schemas/Schema")
            .Append((index % schemas).ToString(CultureInfo.InvariantCulture))
            .AppendLine("'");
    }

    private static void AppendGeneratedSchema(StringBuilder builder, int index)
    {
        string number = index.ToString(CultureInfo.InvariantCulture);

        builder.Append("    Schema").Append(number).AppendLine(":");
        builder.AppendLine("      type: object");
        builder.AppendLine("      required: [id]");
        builder.AppendLine("      properties:");
        builder.AppendLine("        id:");
        builder.AppendLine("          type: string");
        builder.AppendLine("          format: uuid");
        builder.Append("        libelle").Append(number).AppendLine(":");
        builder.AppendLine("          type: string");
        builder.AppendLine("          maxLength: 128");
    }
}
