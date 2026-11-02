namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// La nomenclature d'octets éventuelle, en tête d'un fichier.
/// </summary>
/// <remarks>
/// Trois octets — <c>EF BB BF</c> — que Visual Studio, le Bloc-notes et la plupart des outils Windows
/// écrivent par défaut. Ils ne font pas partie du document : aucun analyseur n'est censé les voir, et le
/// premier caractère utile vient après.
/// <para>
/// Ils étaient sautés à la détection du format, et nulle part ailleurs. Un fichier OpenAPI enregistré
/// sous Windows voyait donc sa première clé lue comme <c>﻿openapi</c>, qui n'est pas
/// <c>openapi</c> : l'outil refusait le fichier en annonçant qu'il ne déclarait pas sa version, ce qui
/// envoyait son auteur chercher une faute dans un fichier juste. Un fichier JSON, lui, échouait à
/// l'analyse.
/// </para>
/// <para>
/// La longueur est rendue plutôt que les octets retirés, et c'est tout l'intérêt : les octets du
/// document restent ceux du fichier. L'empreinte, les étendues et l'épissure qui réécrit une production
/// autour de ce qui change portent donc sur le fichier réel, nomenclature comprise — un document
/// consolidé la conserve, au lieu de perdre trois octets que personne n'a demandé de perdre.
/// </para>
/// </remarks>
internal static class Preamble
{
    private const byte First = 0xEF;
    private const byte Second = 0xBB;
    private const byte Third = 0xBF;

    /// <summary>Le nombre d'octets à sauter avant le premier caractère du document.</summary>
    /// <param name="content">Les octets du fichier.</param>
    /// <returns>Trois, ou zéro.</returns>
    internal static int Length(ReadOnlySpan<byte> content) =>
        content.Length >= 3 && content[0] == First && content[1] == Second && content[2] == Third
            ? 3
            : 0;

    /// <summary>Le texte sans sa nomenclature, s'il en portait une.</summary>
    /// <param name="decoded">Le texte décodé.</param>
    /// <returns>Le texte à analyser.</returns>
    /// <remarks>
    /// <c>Encoding.UTF8.GetString</c> ne retire pas la nomenclature : il la rend comme un caractère
    /// U+FEFF, que l'analyseur YAML prend pour le début d'une clé.
    /// </remarks>
    internal static string Trim(string decoded)
    {
        ArgumentNullException.ThrowIfNull(decoded);

        return decoded.Length > 0 && decoded[0] == '﻿' ? decoded[1..] : decoded;
    }
}
