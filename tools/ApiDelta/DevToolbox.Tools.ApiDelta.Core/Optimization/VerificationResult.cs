namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Ce que la vérification d'une copie optimisée a pu établir.</summary>
/// <param name="Reparsed">La sortie se relit.</param>
/// <param name="Revalidated">Elle respecte la structure de la version qu'elle déclare.</param>
/// <param name="ReferencesResolved">Chacune de ses références se résout.</param>
/// <param name="ContractEquivalent">Elle dit au contrat la même chose que l'original.</param>
/// <param name="Differences">Ce que l'équivalence a trouvé, quand elle a échoué.</param>
/// <remarks>
/// Invariant : aucun artefact optimisé n'est écrit si l'un des quatre est faux. C'est pourquoi les quatre
/// sont des faits séparés et non un booléen : quand la vérification échoue, le développeur doit savoir
/// <em>lequel</em> des quatre a échoué, parce que la suite n'est pas la même selon la réponse.
/// </remarks>
public sealed record VerificationResult(
    bool Reparsed,
    bool Revalidated,
    bool ReferencesResolved,
    bool ContractEquivalent,
    IReadOnlyList<string> Differences)
{
    /// <summary>Une vérification qui n'a pas eu lieu.</summary>
    public static VerificationResult NotRun { get; } = new(false, false, false, false, []);

    /// <summary>Indique si la copie peut être publiée.</summary>
    public bool Established =>
        Reparsed && Revalidated && ReferencesResolved && ContractEquivalent;

    /// <summary>Ce qui a échoué, dit à un lecteur.</summary>
    public string Explanation => Established
        ? "La copie optimisée a été relue, revalidée, ses références résolues, et le contrat est inchangé."
        : "La vérification n'a pas abouti : " + string.Join(" · ", Failures());

    private IEnumerable<string> Failures()
    {
        if (!Reparsed)
        {
            yield return "la sortie ne se relit pas";
        }
        else if (!Revalidated)
        {
            yield return "la sortie ne respecte pas la structure de sa version";
        }
        else if (!ReferencesResolved)
        {
            yield return "une référence de la sortie ne se résout pas";
        }
        else if (!ContractEquivalent)
        {
            yield return "le contrat a changé";
        }

        foreach (string difference in Differences)
        {
            yield return difference;
        }
    }
}
