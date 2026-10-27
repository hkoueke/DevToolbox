namespace DevToolbox.Tools.ApiDelta.Core.Correspondence;

/// <summary>L'appariement entre les opérations d'un contrat et les routes d'une production.</summary>
/// <param name="Entries">Les entrées, une par opération.</param>
/// <param name="IsConfirmed">Indique que le développeur a validé l'ensemble.</param>
/// <remarks>
/// Rien ne se compare avant confirmation, et c'est la règle la plus importante de cette tranche. Aucune
/// convention ne fait le pont entre les deux langages : une correspondance que l'outil aurait inventée
/// sans la montrer rendrait <em>infalsifiable</em> chaque constat posé dessus — le lecteur n'aurait aucun
/// moyen de savoir que la paire comparée n'était pas la bonne.
/// <para>
/// Renoncer est une réponse. Une séance qui s'arrête ici réussit, n'ayant rien comparé, et le rapport dit
/// que la correspondance n'a pas été confirmée. Traiter le renoncement comme un échec pousserait un
/// développeur à confirmer ce qu'il n'a pas lu.
/// </para>
/// </remarks>
public sealed record OperationCorrespondence(
    IReadOnlyList<CorrespondenceEntry> Entries, bool IsConfirmed = false)
{
    /// <summary>Une correspondance vide, non confirmée.</summary>
    public static OperationCorrespondence None { get; } = new([]);

    /// <summary>L'entrée d'une opération, s'il y en a une.</summary>
    /// <param name="operation">Son nom.</param>
    /// <returns>L'entrée, ou rien.</returns>
    public CorrespondenceEntry? For(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        return Entries.FirstOrDefault(entry =>
            string.Equals(entry.Operation, operation, StringComparison.Ordinal));
    }

    /// <summary>
    /// Les entrées sur lesquelles une comparaison peut s'appuyer.
    /// </summary>
    /// <remarks>
    /// Vide tant que l'ensemble n'est pas confirmé, et non « les entrées déclarées en attendant ». Un
    /// appel qui obtiendrait quelques paires d'une correspondance non confirmée comparerait sur une base
    /// que personne n'a validée, ce qui est exactement ce que WR-104 interdit.
    /// </remarks>
    public IReadOnlyList<CorrespondenceEntry> Comparable =>
        IsConfirmed
            ? [.. Entries.Where(entry => entry.HasRoute
                && entry.Origin is CorrespondenceOrigin.Declared or CorrespondenceOrigin.Confirmed)]
            : [];

    /// <summary>Confirme l'ensemble, les propositions devenant des confirmations.</summary>
    /// <returns>La correspondance confirmée.</returns>
    /// <remarks>
    /// Une entrée ambiguë ne devient pas confirmée : elle reste ambiguë, et la couverture le dira. C'est
    /// le seul comportement honnête — confirmer en bloc ne peut pas valoir résolution d'une ambiguïté que
    /// l'outil a précisément renoncé à trancher.
    /// </remarks>
    public OperationCorrespondence Confirm() => new(
        [
            .. Entries.Select(entry => entry.Origin == CorrespondenceOrigin.Proposed
                ? entry with { Origin = CorrespondenceOrigin.Confirmed }
                : entry),
        ],
        IsConfirmed: true);
}
