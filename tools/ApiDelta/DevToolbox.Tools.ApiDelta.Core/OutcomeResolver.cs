using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>
/// Résout l'issue d'une exécution dans le vocabulaire publié.
/// </summary>
/// <remarks>
/// L'ordre des épreuves est le sens même de ce type : la première qui tient gagne, et elle est la plus
/// grave. Un échec de lecture n'est pas un seuil dépassé, un échec de vérification n'est pas un
/// changement cassant, et confondre les deux ferait d'un code d'issue une statistique plutôt qu'un
/// diagnostic.
/// <para>
/// Les constats mis en sourdine n'entrent pas dans le seuil. Une sourdine soustrait un constat à ce
/// calcul, non au rapport : c'est précisément la distinction qui rend une sourdine acceptable.
/// </para>
/// <para>
/// Ce code est affiché et écrit dans le rapport lisible par une machine. Il n'est pas le code de sortie
/// du processus, et cette livraison n'en ajoute aucun : le vocabulaire est fixé maintenant pour qu'une
/// invocation automatisée, le jour où elle sera spécifiée, ajoute une surface et non un sens.
/// </para>
/// </remarks>
public static class OutcomeResolver
{
    /// <summary>Résout l'issue d'une exécution.</summary>
    /// <param name="run">Ce que l'exécution a produit.</param>
    /// <param name="threshold">À partir de quoi l'équipe tient une exécution pour un échec.</param>
    /// <returns>Le code d'issue.</returns>
    public static OutcomeCode Resolve(ApiDeltaRun run, FailureThreshold threshold)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Diagnostics.Any(diagnostic =>
                diagnostic.IsBlocking && IsConfiguration(diagnostic)))
        {
            return OutcomeCode.InvalidUsage;
        }

        if (run.Diagnostics.Any(diagnostic => diagnostic.IsBlocking && !OnCompanionOnly(run, diagnostic)))
        {
            return OutcomeCode.LoadFailure;
        }

        if (run.Optimization is { Applied.Count: > 0, Verification.Established: false })
        {
            return OutcomeCode.OutputFailure;
        }

        if (threshold == FailureThreshold.OnBreakingChange && run.Breaking.Any())
        {
            return OutcomeCode.BreakingChange;
        }

        return Exceeded(run, threshold) ? OutcomeCode.ThresholdExceeded : OutcomeCode.Clean;
    }

    /// <summary>Indique si les constats atteignent le seuil que l'équipe a fixé.</summary>
    /// <remarks>
    /// Un seuil sur l'avertissement couvre aussi l'erreur : une équipe qui échoue sur un avertissement
    /// n'entendait pas laisser passer ce qui est pire.
    /// </remarks>
    private static bool Exceeded(ApiDeltaRun run, FailureThreshold threshold)
    {
        IReadOnlyList<Finding> findings = run.Audit?.Findings ?? [];

        return threshold switch
        {
            FailureThreshold.OnError => findings.Any(finding =>
                finding.Severity == FindingSeverity.Error),

            FailureThreshold.OnWarning => findings.Any(finding =>
                finding.Severity is FindingSeverity.Error or FindingSeverity.Warning),

            FailureThreshold.OnBreakingChange => run.Breaking.Any(),

            _ => false,
        };
    }

    /// <summary>
    /// Indique si un diagnostic reproche quelque chose à la configuration plutôt qu'à la description.
    /// </summary>
    private static bool IsConfiguration(Diagnostic diagnostic) =>
        diagnostic.Code.StartsWith("CONFIG-", StringComparison.Ordinal);

    /// <summary>
    /// Indique qu'un verdict ne porte que sur la structure du fichier compagnon.
    /// </summary>
    /// <remarks>
    /// Un tel verdict n'est pas un échec de lecture : le fichier a été lu, et c'est sa structure qui est
    /// imparfaite. Comme il n'est jamais publié, l'exécution peut aboutir et le document consolidé
    /// répondre de lui-même par sa propre vérification. Annoncer « loadFailure » sur une exécution qui a
    /// écrit un document vérifié apprend à ne plus lire le code d'issue.
    /// <para>
    /// La structure seulement. Un compagnon qui n'a pas pu être <em>analysé</em> porte un autre code, et
    /// celui-là reste un échec de lecture — il n'y avait rien à comparer.
    /// </para>
    /// </remarks>
    private static bool OnCompanionOnly(ApiDeltaRun run, Diagnostic diagnostic) =>
        run.Candidate is { } companion
        && diagnostic.Code is DiagnosticCodes.StructuralViolation
            or DiagnosticCodes.UnresolvedReference
        && string.Equals(diagnostic.Document, companion.Path, StringComparison.Ordinal);
}
