using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>
/// Applique à un constat ce que l'équipe a décidé de sa règle — et rien de plus.
/// </summary>
/// <remarks>
/// Un seul endroit, et c'est le point. Ce que peut faire un jeu de règles tient en une ligne de code :
/// changer une gravité. Ce qu'il ne peut pas faire doit être impossible, et non simplement non écrit —
/// un réglage ne touche ni la classe de sûreté, ni l'équivalence établie, ni la disponibilité d'un
/// remède automatique.
/// <para>
/// La raison n'est pas de principe. La gravité dit combien une équipe s'en soucie ; la sûreté dit ce que
/// l'outil a prouvé. Si un fichier de configuration pouvait promouvoir la seconde, « prouvé sûr »
/// cesserait d'être une preuve pour devenir une opinion, et la seule garantie que l'optimiseur offre
/// disparaîtrait sans que rien ne le signale.
/// </para>
/// </remarks>
public static class RuleSettingsResolver
{
    /// <summary>Applique le réglage d'une règle à l'un de ses constats.</summary>
    /// <param name="finding">Le constat, tel que la règle l'a rendu.</param>
    /// <param name="setting">Ce que l'équipe a décidé.</param>
    /// <returns>Le constat, avec la gravité en vigueur.</returns>
    public static Finding Resolve(Finding finding, RuleSetting setting)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(setting);

        return setting.Severity is { } severity ? finding with { Severity = severity } : finding;
    }

    /// <summary>Applique le réglage d'une règle à tous ses constats.</summary>
    /// <param name="findings">Les constats.</param>
    /// <param name="setting">Ce que l'équipe a décidé.</param>
    /// <returns>Les constats, avec la gravité en vigueur.</returns>
    public static IReadOnlyList<Finding> Resolve(
        IReadOnlyList<Finding> findings,
        RuleSetting setting)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(setting);

        return setting.Severity is null ? findings : [.. findings.Select(f => Resolve(f, setting))];
    }

    /// <summary>Le nombre d'occurrences en deçà duquel une répétition ne vaut pas d'être signalée.</summary>
    /// <param name="setting">Ce que l'équipe a décidé.</param>
    /// <returns>Le seuil, deux à défaut.</returns>
    /// <remarks>
    /// Deux à défaut, parce qu'une seule écriture n'est pas une répétition. Le seuil se relève pour une
    /// équipe que deux occurrences ne dérangent pas, et c'est un réglage de patience, non de sûreté.
    /// </remarks>
    public static int MinimumOccurrences(RuleSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return setting.MinimumOccurrences is { } minimum && minimum > 1 ? minimum : 2;
    }
}
