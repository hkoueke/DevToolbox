namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>
/// La gravité et la sûreté de chaque règle, à défaut de décision contraire.
/// </summary>
/// <remarks>
/// Une table, et non une valeur dispersée dans trente-cinq classes. Le catalogue est publié : un lecteur
/// doit pouvoir vérifier d'un coup d'œil que ce que l'outil fait correspond à ce qui a été annoncé, et un
/// essai le compare ligne à ligne au catalogue.
/// <para>
/// La sûreté portée ici est la meilleure que le remède puisse atteindre. Une règle la dégrade au vu du
/// document — une suppression de composant n'est sûre qu'avec une analyse de références complète — et rien
/// ne l'améliore jamais.
/// </para>
/// </remarks>
public static class RuleDefaults
{
    private static readonly Dictionary<string, RuleDefault> Table = Build();

    /// <summary>Le catalogue entier, par identifiant.</summary>
    public static IReadOnlyDictionary<string, RuleDefault> All => Table;

    /// <summary>Ce qu'une règle vaut par défaut.</summary>
    /// <param name="ruleId">L'identifiant.</param>
    /// <returns>Sa ligne du catalogue.</returns>
    /// <exception cref="KeyNotFoundException">La règle n'est pas au catalogue.</exception>
    public static RuleDefault For(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        return Table.TryGetValue(ruleId, out RuleDefault? found)
            ? found
            : throw new KeyNotFoundException(
                "La règle " + ruleId + " ne figure pas au catalogue des règles.");
    }

    private static Dictionary<string, RuleDefault> Build()
    {
        Dictionary<string, RuleDefault> table = new(StringComparer.Ordinal);

        Validity(table);
        Redundancy(table);
        Consistency(table);
        Governance(table);

        return table;
    }

    private static void Validity(Dictionary<string, RuleDefault> table)
    {
        Add(table, "AUD-001", FindingSeverity.Error, SafetyClass.Unsafe);
        Add(table, "AUD-002", FindingSeverity.Error, SafetyClass.Unsafe);

        // Sûre une fois l'analyse de références complète et aucun motif de conservation apparié ; la règle
        // dégrade elle-même quand l'une des deux conditions manque.
        Add(table, "AUD-003", FindingSeverity.Warning, SafetyClass.Safe);

        Add(table, "AUD-004", FindingSeverity.Error, SafetyClass.Unsafe);

        // Erreur pour un doublon, avertissement pour un manque : la règle choisit par constat.
        Add(table, "AUD-005", FindingSeverity.Error, SafetyClass.Unsafe);

        Add(table, "AUD-006", FindingSeverity.Warning, SafetyClass.Unsafe);
        Add(table, "AUD-007", FindingSeverity.Warning, SafetyClass.ReviewRequired);
        Add(table, "AUD-008", FindingSeverity.Warning, SafetyClass.ReviewRequired);
        Add(table, "AUD-009", FindingSeverity.Warning, SafetyClass.ReviewRequired);
        Add(table, "AUD-010", FindingSeverity.Warning, SafetyClass.ReviewRequired);
    }

    private static void Redundancy(Dictionary<string, RuleDefault> table)
    {
        Add(table, "AUD-100", FindingSeverity.Warning, SafetyClass.Safe);
        Add(table, "AUD-101", FindingSeverity.Warning, SafetyClass.Safe);
        Add(table, "AUD-102", FindingSeverity.Warning, SafetyClass.Safe);
        Add(table, "AUD-103", FindingSeverity.Warning, SafetyClass.Safe);

        // Avertissement pour un doublon exact, information pour un quasi-doublon ; sûre seulement sous le
        // même nom, et à relire dès que les noms diffèrent.
        Add(table, "AUD-104", FindingSeverity.Warning, SafetyClass.Safe);

        Add(table, "AUD-105", FindingSeverity.Information, SafetyClass.Safe);

        // Jamais fusionnés : deux énumérations identiques peuvent porter deux sens métier.
        Add(table, "AUD-106", FindingSeverity.Information, SafetyClass.ReviewRequired);

        Add(table, "AUD-107", FindingSeverity.Information, SafetyClass.ReviewRequired);
    }

    private static void Consistency(Dictionary<string, RuleDefault> table)
    {
        Add(table, "AUD-200", FindingSeverity.Information, SafetyClass.Safe);
        Add(table, "AUD-201", FindingSeverity.Information, SafetyClass.Safe);
        Add(table, "AUD-202", FindingSeverity.Information, SafetyClass.Safe);

        // Signalés, jamais fusionnés : la casse d'un en-tête peut compter pour une passerelle.
        Add(table, "AUD-203", FindingSeverity.Warning, SafetyClass.Unsafe);

        Add(table, "AUD-204", FindingSeverity.Information, SafetyClass.Unsafe);
        Add(table, "AUD-205", FindingSeverity.Warning, SafetyClass.ReviewRequired);
        Add(table, "AUD-206", FindingSeverity.Information, SafetyClass.ReviewRequired);
        Add(table, "AUD-207", FindingSeverity.Information, SafetyClass.Unsafe);
        Add(table, "AUD-208", FindingSeverity.Error, SafetyClass.Unsafe);
        Add(table, "AUD-209", FindingSeverity.Information, SafetyClass.Unsafe);
        Add(table, "AUD-210", FindingSeverity.Warning, SafetyClass.Unsafe);
    }

    private static void Governance(Dictionary<string, RuleDefault> table)
    {
        Add(table, "AUD-300", FindingSeverity.Information, SafetyClass.Unsafe);
        Add(table, "AUD-301", FindingSeverity.Warning, SafetyClass.Unsafe);
        Add(table, "AUD-302", FindingSeverity.Warning, SafetyClass.Unsafe);

        // Erreur ou avertissement selon la politique : la règle choisit selon ce que le jeu de règles dit.
        Add(table, "AUD-303", FindingSeverity.Error, SafetyClass.Unsafe);

        Add(table, "AUD-304", FindingSeverity.Warning, SafetyClass.Unsafe);
        Add(table, "AUD-305", FindingSeverity.Information, SafetyClass.Unsafe);
        Add(table, "AUD-306", FindingSeverity.Warning, SafetyClass.Unsafe);
    }

    private static void Add(
        Dictionary<string, RuleDefault> table,
        string ruleId,
        FindingSeverity severity,
        SafetyClass safety) =>
        table[ruleId] = new RuleDefault(ruleId, severity, safety);
}
