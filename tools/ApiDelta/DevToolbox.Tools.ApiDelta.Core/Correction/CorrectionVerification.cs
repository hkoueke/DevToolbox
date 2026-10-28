namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce que la vérification de la production consolidée a pu établir.</summary>
/// <param name="Reparsed">Le document se relit.</param>
/// <param name="Revalidated">Il respecte la structure de la version qu'il déclare.</param>
/// <param name="ReferencesResolved">Chacune de ses références se résout.</param>
/// <param name="UnselectedUntouched">
/// Tout ce que la sélection n'avait pas nommé est identique, à l'octet, à ce que la production disait.
/// </param>
/// <param name="SelectedAsChosen">
/// Chaque endroit que la sélection avait nommé a changé, et aucun autre n'a changé.
/// </param>
/// <param name="Complaints">Ce qui a été trouvé à dire, nommé.</param>
/// <remarks>
/// Les deux dernières épreuves sont la raison d'être de ce type, et elles vont par paire. « Rien d'autre
/// n'a bougé » sans « ce qui devait bouger a bougé » laisserait passer une correction qui n'aurait rien
/// fait ; l'inverse laisserait passer une correction qui aurait fait autre chose en plus. Ensemble, elles
/// disent que le diff qu'un développeur lira est exactement la sélection qu'il a faite.
/// <para>
/// Cinq épreuves séparées, parce qu'un échec doit dire laquelle : « le document ne se relit pas » et
/// « une route que personne n'avait retenue a changé » ne demandent pas la même suite.
/// </para>
/// <para>
/// Il y en avait une sixième, qui vérifiait que chaque élément neuf portait sa trace d'origine dans le
/// document. Les traces ne sont plus écrites — le fichier produit est destiné à devenir un contrat
/// publié et ne porte que de l'OpenAPI — et cette épreuve a donc été retirée plutôt que laissée à
/// « toujours vraie », ce qui en aurait fait une épreuve qui ne prouve rien. Ce que les traces disaient
/// se lit dans le rapport, et l'épreuve qui compte est restée : rien n'a bougé hors de la sélection.
/// </para>
/// </remarks>
public sealed record CorrectionVerification(
    bool Reparsed,
    bool Revalidated,
    bool ReferencesResolved,
    bool UnselectedUntouched,
    bool SelectedAsChosen,
    IReadOnlyList<string> Complaints)
{
    /// <summary>Une vérification qui n'a pas eu lieu.</summary>
    public static CorrectionVerification NotRun { get; } =
        new(false, false, false, false, false, []);

    /// <summary>Indique si la production consolidée peut être publiée.</summary>
    public bool Established =>
        Reparsed && Revalidated && ReferencesResolved && UnselectedUntouched && SelectedAsChosen;

    /// <summary>Ce qui a échoué, dit à un lecteur.</summary>
    public string Explanation => Established
        ? "La production consolidée a été relue, revalidée, ses références résolues ; tout ce qui n'avait "
            + "pas été retenu y est identique à l'octet, et ce qui l'avait été a changé."
        : "La vérification n'a pas abouti : " + string.Join(" · ", Failures());

    private IEnumerable<string> Failures()
    {
        if (!Reparsed)
        {
            yield return "le document ne se relit pas";
        }
        else if (!Revalidated)
        {
            yield return "le document ne respecte pas la structure de sa version";
        }
        else if (!ReferencesResolved)
        {
            yield return "une référence du document ne se résout pas";
        }
        else if (!UnselectedUntouched)
        {
            yield return "quelque chose que personne n'avait retenu a changé";
        }
        else if (!SelectedAsChosen)
        {
            yield return "ce qui avait été retenu n'a pas changé comme demandé";
        }

        foreach (string complaint in Complaints)
        {
            yield return complaint;
        }
    }
}
