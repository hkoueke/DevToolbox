using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>
/// Appose sur l'extrait une marque qui dit ce qu'il est.
/// </summary>
/// <remarks>
/// Elle existe parce qu'une description réduite à trois routes est indiscernable de la description d'une
/// interface qui en a trois, et que la différence compte la première fois que l'une est relue ou publiée
/// dans la croyance qu'elle est complète.
/// <para>
/// Une seule entrée, à la racine, et rien d'autre n'est touché. En particulier, si le document portait
/// déjà une extension de ce nom — improbable, mais c'est précisément le cas où une écriture distraite
/// ferait disparaître une donnée dont l'outil ne sait rien — la marque n'est pas apposée.
/// </para>
/// </remarks>
public static class ProvenanceStamp
{
    /// <summary>Le nom de l'extension, qui fait partie de la sortie.</summary>
    public const string Field = "x-apidelta-extract";

    /// <summary>Appose la marque, si elle est demandée et si la place est libre.</summary>
    /// <param name="document">L'extrait monté.</param>
    /// <param name="provenance">D'où il vient.</param>
    /// <returns>L'extrait, marqué ou tel quel.</returns>
    public static SpecNode Apply(SpecNode document, ExtractProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(provenance);

        if (document is not MappingNode root || root.Find(Field) is not null)
        {
            return document;
        }

        return TreeEdits.Mapping(
            SpecPointer.Root,
            [.. root.Entries, new MappingEntry(TreeEdits.Key(Field), Mark(provenance))]);
    }

    /// <summary>La marque elle-même.</summary>
    private static MappingNode Mark(ExtractProvenance provenance)
    {
        SpecPointer location = SpecPointer.Root.Child(Field);

        return TreeEdits.Mapping(
            location,
            [
                Entry("source", provenance.SourcePath),
                Entry("sourceSha256", provenance.SourceHash),
                Entry(
                    "extractedAt",
                    provenance.ExtractedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                new MappingEntry(
                    TreeEdits.Key("operations"),
                    TreeEdits.Sequence(
                        location.Child("operations"),
                        [
                            .. provenance.Selectors
                                .Select(selector => selector.Display)
                                .OrderBy(display => display, StringComparer.Ordinal)
                                .Select(display => Scalar(display)),
                        ])),
                Entry(
                    "note",
                    "Description partielle : seules les opérations listées ci-dessus sont présentes."),
            ]);
    }

    private static MappingEntry Entry(string key, string value) =>
        new(TreeEdits.Key(key), Scalar(value));

    private static ScalarNode Scalar(string value) => TreeEdits.Key(value);
}
