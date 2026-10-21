using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>Plusieurs écritures de la même chose.</summary>
/// <param name="Fingerprint">L'empreinte que ses membres partagent.</param>
/// <param name="Members">Les membres, dans l'ordre où le document les écrit.</param>
public sealed record DuplicateGroup(string Fingerprint, IReadOnlyList<SpecNode> Members)
{
    /// <summary>Le nombre d'occurrences.</summary>
    public int Count => Members.Count;

    /// <summary>Le nombre d'octets qu'occupe une occurrence, prise comme la plus grande.</summary>
    /// <remarks>
    /// La plus grande, et non la moyenne : l'économie annoncée doit être une borne qu'on ne dépasse pas
    /// par surprise, et c'est la seule des deux qu'on peut tenir.
    /// </remarks>
    public long OccurrenceBytes => SizeMeasurement.Largest(Members);

    /// <summary>Les emplacements des membres.</summary>
    public IReadOnlyList<SpecPointer> Locations => [.. Members.Select(member => member.Location)];
}
