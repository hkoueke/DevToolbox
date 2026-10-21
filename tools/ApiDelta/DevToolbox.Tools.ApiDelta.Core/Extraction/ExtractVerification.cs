namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>Ce que la vérification d'un extrait a pu établir.</summary>
/// <param name="Reparsed">L'extrait se relit.</param>
/// <param name="Revalidated">Il respecte la structure de la version qu'il déclare.</param>
/// <param name="ReferencesResolved">Chacune de ses références se résout.</param>
/// <param name="ContainsNothingUnselected">Il ne porte aucun chemin ni aucune méthode non retenus.</param>
/// <param name="OperationsEquivalent">
/// Chaque opération retenue dit, sous forme effective, la même chose que dans la source.
/// </param>
/// <param name="Differences">Ce que la comparaison a trouvé, quand elle a échoué.</param>
/// <remarks>
/// Invariant : aucun extrait n'est écrit si l'un des cinq est faux, et la comparaison porte sur les
/// octets émis. Les cinq sont séparés parce qu'un échec doit dire lequel : « l'extrait ne se relit pas »
/// et « il a perdu un paramètre de chemin » ne demandent pas la même suite.
/// </remarks>
public sealed record ExtractVerification(
    bool Reparsed,
    bool Revalidated,
    bool ReferencesResolved,
    bool ContainsNothingUnselected,
    bool OperationsEquivalent,
    IReadOnlyList<string> Differences)
{
    /// <summary>Une vérification qui n'a pas eu lieu.</summary>
    public static ExtractVerification NotRun { get; } =
        new(false, false, false, false, false, []);

    /// <summary>Indique si l'extrait peut être publié.</summary>
    public bool Established =>
        Reparsed && Revalidated && ReferencesResolved && ContainsNothingUnselected
        && OperationsEquivalent;

    /// <summary>Ce qui a échoué, dit à un lecteur.</summary>
    public string Explanation => Established
        ? "L'extrait a été relu, revalidé, ses références résolues, et chaque opération retenue y dit "
            + "la même chose que dans la source."
        : "La vérification n'a pas abouti : " + string.Join(" · ", Failures());

    private IEnumerable<string> Failures()
    {
        if (!Reparsed)
        {
            yield return "l'extrait ne se relit pas";
        }
        else if (!Revalidated)
        {
            yield return "l'extrait ne respecte pas la structure de sa version";
        }
        else if (!ReferencesResolved)
        {
            yield return "une référence de l'extrait ne se résout pas";
        }
        else if (!ContainsNothingUnselected)
        {
            yield return "l'extrait porte quelque chose qui n'avait pas été retenu";
        }
        else if (!OperationsEquivalent)
        {
            yield return "une opération retenue ne dit plus la même chose";
        }

        foreach (string difference in Differences)
        {
            yield return difference;
        }
    }
}
