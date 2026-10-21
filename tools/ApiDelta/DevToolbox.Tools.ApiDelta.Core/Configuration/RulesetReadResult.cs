using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Un jeu de règles lu, ou de quoi expliquer son refus.</summary>
/// <param name="Ruleset">Le jeu de règles, absent quand le fichier n'est pas applicable.</param>
/// <param name="Diagnostics">Ce qu'il y a à dire, situé dans le fichier.</param>
/// <remarks>
/// Invariant : une instance ne porte un jeu de règles que si le fichier a été compris en entier. Une clé
/// inconnue, un identifiant de règle inexistant, une gravité qui n'existe pas — chacun empêche le jeu
/// d'exister, plutôt que de produire une configuration à moitié appliquée dont personne ne saurait
/// laquelle des deux moitiés a pris effet.
/// </remarks>
public sealed record RulesetReadResult(Ruleset? Ruleset, IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Un refus.</summary>
    /// <param name="diagnostics">Ce qui l'explique.</param>
    /// <returns>Le résultat.</returns>
    public static RulesetReadResult Refused(IReadOnlyList<Diagnostic> diagnostics) =>
        new(null, diagnostics);

    /// <summary>Une lecture aboutie.</summary>
    /// <param name="ruleset">Le jeu de règles.</param>
    /// <returns>Le résultat.</returns>
    public static RulesetReadResult Read(Ruleset ruleset) => new(ruleset, []);

    /// <summary>Indique si le fichier a été compris en entier.</summary>
    public bool IsApplicable => Ruleset is not null;
}
