using System.Globalization;
using System.Text;

namespace DevToolbox.Tests.Tools.ApiDelta.Corpus;

/// <summary>
/// Engendre des descriptions de la taille qu'on demande.
/// </summary>
/// <remarks>
/// Engendrées plutôt que rangées dans le corpus : un fichier de quinze mégaoctets dans le dépôt
/// alourdirait chaque clonage pour servir deux essais. Et la forme compte plus que le contenu — ce qui
/// est éprouvé ici est la tenue sous le nombre, pas la justesse d'un cas particulier.
/// </remarks>
internal static class GeneratedDescription
{
    /// <summary>
    /// Une description de la taille demandée : autant d'opérations, autant de schémas, et une chaîne de
    /// références qui les traverse.
    /// </summary>
    /// <param name="operations">Le nombre d'opérations.</param>
    /// <param name="schemas">Le nombre de schémas.</param>
    /// <returns>Le texte YAML.</returns>
    internal static string Wide(int operations, int schemas)
    {
        StringBuilder builder = new();

        builder.AppendLine("openapi: 3.0.3");
        builder.AppendLine("info:");
        builder.AppendLine("  title: Description engendree");
        builder.AppendLine("  version: 3.4.1");
        builder.AppendLine("security:");
        builder.AppendLine("  - bearerAuth: []");
        builder.AppendLine("tags:");
        builder.AppendLine("  - name: Engendre");
        builder.AppendLine("    description: Tout vient d ici.");
        builder.AppendLine("paths:");

        for (int rank = 0; rank < operations; rank++)
        {
            Operation(builder, rank, schemas);
        }

        builder.AppendLine("components:");
        builder.AppendLine("  securitySchemes:");
        builder.AppendLine("    bearerAuth:");
        builder.AppendLine("      type: http");
        builder.AppendLine("      scheme: bearer");
        builder.AppendLine("  schemas:");

        for (int rank = 0; rank < schemas; rank++)
        {
            Schema(builder, rank, schemas);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Une description dont l'imbrication dépasse la profondeur demandée.
    /// </summary>
    /// <remarks>
    /// Un seul schéma, mais profond : c'est la forme qui fait déborder une pile, et la seule façon
    /// d'établir que la limite est atteinte et nommée au lieu d'être découverte par un plantage.
    /// </remarks>
    /// <param name="depth">La profondeur d'imbrication.</param>
    /// <returns>Le texte YAML.</returns>
    internal static string Deep(int depth)
    {
        StringBuilder builder = new();

        builder.AppendLine("openapi: 3.0.3");
        builder.AppendLine("info:");
        builder.AppendLine("  title: Description profonde");
        builder.AppendLine("  version: 1.0.0");
        builder.AppendLine("paths: {}");
        builder.AppendLine("components:");
        builder.AppendLine("  schemas:");
        builder.AppendLine("    Profond:");

        string indent = "      ";

        for (int level = 0; level < depth; level++)
        {
            builder.Append(indent).AppendLine("type: object");
            builder.Append(indent).AppendLine("properties:");
            builder.Append(indent).AppendLine("  niveau:");

            indent += "    ";
        }

        builder.Append(indent).AppendLine("type: string");

        return builder.ToString();
    }

    private static void Operation(StringBuilder builder, int rank, int schemas)
    {
        string number = rank.ToString(CultureInfo.InvariantCulture);

        builder.Append("  /ressource").Append(number).AppendLine(":");
        builder.AppendLine("    parameters:");
        builder.AppendLine("      - name: X-serviceEmetteur");
        builder.AppendLine("        in: header");
        builder.AppendLine("        required: true");
        builder.AppendLine("        schema:");
        builder.AppendLine("          type: string");
        builder.AppendLine("    get:");
        builder.AppendLine("      tags: [Engendre]");
        builder.Append("      summary: Lit la ressource ").AppendLine(number);
        builder.Append("      operationId: lireRessource").AppendLine(number);
        builder.AppendLine("      responses:");
        builder.AppendLine("        '200':");
        builder.AppendLine("          description: La ressource.");
        builder.AppendLine("          content:");
        builder.AppendLine("            application/json:");
        builder.AppendLine("              schema:");
        builder.Append("                $ref: '#/components/schemas/Schema")
            .Append((rank % schemas).ToString(CultureInfo.InvariantCulture)).AppendLine("'");
        builder.AppendLine("        '400':");
        builder.AppendLine("          description: La requete est invalide.");
    }

    private static void Schema(StringBuilder builder, int rank, int schemas)
    {
        string number = rank.ToString(CultureInfo.InvariantCulture);

        builder.Append("    Schema").Append(number).AppendLine(":");
        builder.AppendLine("      type: object");
        builder.AppendLine("      required: [identifiant]");
        builder.AppendLine("      properties:");
        builder.AppendLine("        identifiant:");
        builder.AppendLine("          type: string");
        builder.AppendLine("          format: uuid");
        builder.AppendLine("        libelle:");
        builder.AppendLine("          type: string");
        builder.AppendLine("          maxLength: 120");

        // Une chaîne de références : chaque schéma désigne le suivant, si bien que la fermeture
        // transitive a réellement du chemin à parcourir.
        builder.AppendLine("        suivant:");
        builder.Append("          $ref: '#/components/schemas/Schema")
            .Append(((rank + 1) % schemas).ToString(CultureInfo.InvariantCulture)).AppendLine("'");
    }
}
