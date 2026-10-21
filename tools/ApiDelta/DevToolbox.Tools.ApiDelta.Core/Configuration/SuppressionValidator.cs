using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>
/// Vérifie les sourdines avant que l'exécution ne commence.
/// </summary>
/// <remarks>
/// Avant, et c'est la seule place qui convient. Une sourdine sans justification découverte en cours
/// d'examen laisserait le choix entre deux mauvaises réponses : l'honorer, et taire un constat que
/// personne n'a motivé ; l'ignorer, et rapporter un constat que l'équipe croyait tu. Refuser de démarrer
/// est la troisième, et c'est la seule qui dit la vérité.
/// </remarks>
public static class SuppressionValidator
{
    /// <summary>Valide une liste de sourdines.</summary>
    /// <param name="suppressions">Ce que le fichier porte.</param>
    /// <param name="ruleset">Ce que l'équipe a décidé, dont l'exigence de justification.</param>
    /// <param name="path">Le fichier, nommé dans chaque diagnostic.</param>
    /// <returns>Ce qu'il y a à dire, vide quand tout tient.</returns>
    public static IReadOnlyList<Diagnostic> Validate(
        IReadOnlyList<Suppression> suppressions,
        Ruleset ruleset,
        string path)
    {
        ArgumentNullException.ThrowIfNull(suppressions);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        List<Diagnostic> found = [];

        foreach (Suppression suppression in suppressions)
        {
            if (!RuleDefaults.All.ContainsKey(suppression.RuleId))
            {
                found.Add(Invalid(
                    path,
                    "la règle « " + suppression.RuleId + " » n'existe pas. Une sourdine sur un "
                    + "identifiant inconnu ne tait rien, et laisse croire le contraire."));
            }

            if (string.IsNullOrWhiteSpace(suppression.Location))
            {
                found.Add(Invalid(
                    path, "une sourdine sur « " + suppression.RuleId + " » n'indique pas d'emplacement."));
            }

            if (string.IsNullOrWhiteSpace(suppression.Owner))
            {
                found.Add(Invalid(
                    path,
                    "la sourdine sur « " + suppression.RuleId + " » n'a pas de propriétaire : "
                    + "personne ne saura à qui la demander."));
            }

            if (ruleset.RequireSuppressionJustification
                && string.IsNullOrWhiteSpace(suppression.Justification))
            {
                found.Add(Invalid(
                    path,
                    "la sourdine sur « " + suppression.RuleId + " » en "
                    + suppression.Location + " n'est pas justifiée."));
            }
        }

        return found;
    }

    private static Diagnostic Invalid(string path, string message) =>
        new(
            ConfigurationCodes.Invalid,
            DiagnosticSeverity.Error,
            "Dans « " + path + " » : " + message);
}
