namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Une mise en sourdine : ce qu'on tait, où, pourquoi, et jusqu'à quand.</summary>
/// <param name="RuleId">La règle visée.</param>
/// <param name="Location">Un pointeur exact, ou un motif sur un pointeur.</param>
/// <param name="Justification">Pourquoi ce constat est accepté tel quel.</param>
/// <param name="Owner">À qui le demander.</param>
/// <param name="Expires">La date après laquelle elle ne vaut plus, s'il y en a une.</param>
/// <remarks>
/// Les quatre premiers champs sont là pour qu'une mise en sourdine reste une décision et ne devienne pas
/// de l'archéologie. Sans justification, personne ne saura dans deux ans si elle tient encore ; sans
/// propriétaire, elle est orpheline, et c'est ainsi qu'un fichier de sourdines finit par ne plus être
/// relu du tout.
/// </remarks>
public sealed record Suppression(
    string RuleId,
    string Location,
    string? Justification,
    string? Owner,
    DateOnly? Expires = null)
{
    /// <summary>Indique si elle a cessé de valoir à une date donnée.</summary>
    /// <param name="today">Le jour de l'exécution.</param>
    /// <returns><see langword="true"/> si l'échéance est passée.</returns>
    public bool HasExpired(DateOnly today) => Expires is { } expires && expires < today;
}
