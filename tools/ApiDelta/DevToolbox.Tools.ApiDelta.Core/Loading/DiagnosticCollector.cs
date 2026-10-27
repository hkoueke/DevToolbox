using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Steps;

namespace DevToolbox.Tools.ApiDelta.Core.Loading;

/// <summary>Rassemble dans le contexte ce que la lecture et la validation ont trouvé à dire.</summary>
/// <remarks>
/// Tout y est consigné, y compris les avertissements d'une lecture réussie : le rapport les porte, et un
/// scalaire ambigu signalé pendant la lecture puis oublié avant l'écriture ne servirait à personne.
/// </remarks>
public static class DiagnosticCollector
{
    /// <summary>
    /// Ajoute des diagnostics à ceux de l'exécution, en nommant le fichier dont ils parlent.
    /// </summary>
    /// <param name="context">Le contexte de l'exécution.</param>
    /// <param name="diagnostics">Ce qu'il y a à ajouter.</param>
    /// <param name="document">Le fichier que l'étape lisait, quand elle en lisait un.</param>
    /// <remarks>
    /// Le timbre est posé ici plutôt que chez chaque producteur, et c'est le bon endroit : seul l'appelant
    /// sait <em>lequel</em> des deux fichiers il était en train de lire, là où un lecteur ne connaît que
    /// le flux d'octets qu'on lui a tendu. Celui qui sait déjà — le lecteur de contrat, qui ouvre vingt
    /// documents — le dit lui-même, et son timbre n'est pas écrasé.
    /// </remarks>
    public static void Add(
        ToolRunContext context, IEnumerable<Diagnostic> diagnostics, string? document = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostics);

        List<Diagnostic> all =
            context.Get<List<Diagnostic>>(ApiDeltaContextKeys.Diagnostics) ?? [];

        all.AddRange(document is null
            ? diagnostics
            : diagnostics.Select(diagnostic => diagnostic.Document is null
                ? diagnostic with { Document = document }
                : diagnostic));

        context.Set(ApiDeltaContextKeys.Diagnostics, all);
    }

    /// <summary>Tout ce qui a été consigné pour cette exécution.</summary>
    /// <param name="context">Le contexte de l'exécution.</param>
    /// <returns>Les diagnostics, dans l'ordre où ils sont venus.</returns>
    public static IReadOnlyList<Diagnostic> Of(ToolRunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Get<List<Diagnostic>>(ApiDeltaContextKeys.Diagnostics) ?? [];
    }

    /// <summary>
    /// Résume des diagnostics en une phrase affichable, pour le message d'échec d'une étape.
    /// </summary>
    /// <param name="diagnostics">Ce qu'il faut résumer.</param>
    /// <returns>Le premier message, et le compte de ceux qui suivent.</returns>
    /// <remarks>
    /// Le premier message et un compte, plutôt que tout : l'invite de reprise tient sur quelques lignes, et
    /// la liste complète a sa place dans la vue des diagnostics et dans le rapport.
    /// </remarks>
    public static string Summarise(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        IReadOnlyList<Diagnostic> all = [.. diagnostics];

        if (all.Count == 0)
        {
            return "aucun détail n'a été rapporté.";
        }

        string first = all[0].Message;

        return all.Count == 1 ? first : first + $" (et {all.Count - 1} autre(s))";
    }
}
