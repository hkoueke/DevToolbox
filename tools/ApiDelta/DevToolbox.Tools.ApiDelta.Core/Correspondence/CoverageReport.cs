namespace DevToolbox.Tools.ApiDelta.Core.Correspondence;

/// <summary>
/// Ce que la production couvre du contrat de service, et ce qu'elle ne couvre pas.
/// </summary>
/// <remarks>
/// Calculée depuis la correspondance confirmée <em>et rien d'autre</em>. En particulier jamais depuis le
/// comparateur : la couverture et la compatibilité répondent à deux questions, portent deux certitudes, et
/// sont traitées par deux personnes différentes. La couverture dit ce qui manque, avec certitude. La
/// compatibilité dit si ce qui existe tient encore, avec les réserves de la projection.
/// <para>
/// Les mêler ferait apparaître un manque parmi les ruptures. Rien n'est cassé quand une opération n'a pas
/// de route : quelque chose est absent, et les deux nouvelles ne s'adressent pas au même lecteur ni au
/// même calendrier.
/// </para>
/// </remarks>
public sealed record CoverageReport(IReadOnlyList<CoverageEntry> Entries)
{
    /// <summary>Un rapport vide : la correspondance n'a pas été confirmée.</summary>
    public static CoverageReport NotEstablished { get; } = new([]);

    /// <summary>Les opérations auxquelles la production répond.</summary>
    public IEnumerable<CoverageEntry> Covered =>
        Entries.Where(entry => entry.State == CoverageState.Covered);

    /// <summary>Les opérations auxquelles la production ne répond nulle part.</summary>
    public IEnumerable<CoverageEntry> Gaps =>
        Entries.Where(entry => entry.State == CoverageState.Absent);

    /// <summary>Les opérations dont la contrepartie n'a pas été tranchée.</summary>
    public IEnumerable<CoverageEntry> Ambiguous =>
        Entries.Where(entry => entry.State == CoverageState.Ambiguous);

    /// <summary>
    /// Indique que la couverture a pu être établie.
    /// </summary>
    /// <remarks>
    /// Un rapport sans entrée n'est pas un rapport annonçant une couverture totale : c'est un rapport qui
    /// n'a rien établi. La distinction est la même que celle entre « indéterminé » et « absent » ailleurs
    /// dans cet outil, et elle compte autant.
    /// </remarks>
    public bool IsEstablished => Entries.Count > 0;

    /// <summary>
    /// Établit la couverture depuis une correspondance confirmée et les routes de la production.
    /// </summary>
    /// <param name="correspondence">La correspondance, qui doit être confirmée.</param>
    /// <param name="operations">Les opérations du contrat.</param>
    /// <param name="routes">Les routes que la production porte.</param>
    /// <returns>Le rapport, ou celui qui n'a rien établi si rien n'est confirmé.</returns>
    public static CoverageReport Establish(
        OperationCorrespondence correspondence,
        IReadOnlyList<string> operations,
        IReadOnlySet<string> routes)
    {
        ArgumentNullException.ThrowIfNull(correspondence);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(routes);

        if (!correspondence.IsConfirmed)
        {
            // Rien n'a été confirmé, donc rien n'est établi. Rendre « tout absent » serait une nouvelle
            // alarmante et fausse ; rendre « tout couvert » serait rassurant et faux.
            return NotEstablished;
        }

        List<CoverageEntry> entries = [];

        foreach (string operation in operations)
        {
            entries.Add(Entry(operation, correspondence.For(operation), routes));
        }

        return new CoverageReport(entries);
    }

    /// <summary>L'état d'une opération, et de quoi agir dessus.</summary>
    private static CoverageEntry Entry(
        string operation, CorrespondenceEntry? paired, IReadOnlySet<string> routes)
    {
        if (paired is null || !paired.HasRoute)
        {
            return new CoverageEntry(operation, CoverageState.Absent, null, Needed(operation));
        }

        if (paired.Origin == CorrespondenceOrigin.Ambiguous)
        {
            return new CoverageEntry(
                operation,
                CoverageState.Ambiguous,
                paired.Route,
                "Plusieurs routes pourraient répondre à cette opération : "
                    + string.Join(", ", new[] { paired.Route }.Concat(paired.Alternatives))
                    + ". Il faut en désigner une, ou la déclarer absente.");
        }

        return routes.Contains(paired.Route)
            ? new CoverageEntry(operation, CoverageState.Covered, paired.Route, null)
            : new CoverageEntry(
                operation,
                CoverageState.Absent,
                paired.Route,
                "La correspondance désigne « " + paired.Route + " », que la production ne porte pas. "
                    + Needed(operation));
    }

    /// <summary>Ce que la production devrait porter, en des termes sur lesquels agir.</summary>
    /// <remarks>
    /// Nommer l'opération ne suffit pas : un développeur doit savoir ce qu'il lui faut écrire. Le message
    /// dit donc la route proposée plutôt que de constater le manque — c'est la différence entre un
    /// rapport qu'on lit et un rapport dont on part.
    /// </remarks>
    private static string Needed(string operation) =>
        "La production devrait porter une route répondant à « " + operation
            + " » : la proposition de l'outil est " + Projection.RouteDerivation.Derive(operation).Display
            + ", que l'écran de sélection permet d'insérer.";
}
