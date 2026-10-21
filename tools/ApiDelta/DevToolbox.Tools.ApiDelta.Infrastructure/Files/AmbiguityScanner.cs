using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Relève ce qui a été lu sans que la lecture soit univoque, et ce qui semble encodé là où du texte
/// décodé est attendu.
/// </summary>
/// <remarks>
/// Des avertissements, non des erreurs : le document est lisible, mais l'auteur n'a peut-être pas écrit ce
/// qu'il croyait. Rien n'est corrigé — décoder une entité HTML ou quoter un <c>no</c> à la place de
/// l'auteur changerait le contrat qu'il a décrit.
/// </remarks>
internal static class AmbiguityScanner
{
    private static readonly string[] SuspiciousEntities = ["&gt;", "&lt;", "&amp;", "&quot;", "&#39;"];

    internal static IReadOnlyList<Diagnostic> Scan(SpecNode root)
    {
        List<Diagnostic> found = [];

        Walk(root, found);

        return found;
    }

    private static void Walk(SpecNode node, List<Diagnostic> found)
    {
        switch (node)
        {
            case ScalarNode scalar:
                Inspect(scalar, found);
                break;

            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    Inspect(entry.Key, found);
                    Walk(entry.Value, found);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    Walk(item, found);
                }

                break;
        }
    }

    private static void Inspect(ScalarNode scalar, List<Diagnostic> found)
    {
        if (scalar.Resolved.Kind == ScalarValueKind.Ambiguous)
        {
            found.Add(new Diagnostic(
                DiagnosticCodes.AmbiguousScalar,
                DiagnosticSeverity.Warning,
                scalar.Resolved.AmbiguityReason!,
                scalar.Location,
                scalar.Position));
        }

        string? entity = SuspiciousEntities.FirstOrDefault(
            candidate => scalar.Text.Contains(candidate, StringComparison.Ordinal));

        if (entity is not null)
        {
            found.Add(new Diagnostic(
                DiagnosticCodes.SuspiciousEncoding,
                DiagnosticSeverity.Warning,
                $"Ce texte contient « {entity} » : il a probablement été encodé une fois de trop. "
                    + "Rien n'est décodé ici, car décoder changerait ce que la description dit.",
                scalar.Location,
                scalar.Position));
        }
    }
}
