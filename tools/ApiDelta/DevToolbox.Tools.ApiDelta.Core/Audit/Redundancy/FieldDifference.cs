using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;

/// <summary>
/// Nomme les champs par lesquels deux objets diffèrent.
/// </summary>
/// <remarks>
/// « Ces deux schémas se ressemblent » ne se tranche pas ; « ils diffèrent par <c>maxLength</c> et
/// <c>description</c> » se tranche en dix secondes. C'est tout l'écart entre un constat et une
/// recommandation.
/// </remarks>
internal static class FieldDifference
{
    /// <summary>Les champs par lesquels deux objets diffèrent, triés.</summary>
    /// <param name="left">Le premier.</param>
    /// <param name="right">Le second.</param>
    /// <param name="fingerprinter">Compare les valeurs par leur empreinte.</param>
    /// <param name="version">La version déclarée.</param>
    /// <returns>Les noms des champs.</returns>
    internal static IReadOnlyList<string> Between(
        SpecNode left,
        SpecNode right,
        IFingerprinter fingerprinter,
        OpenApiVersion version)
    {
        if (left is not MappingNode first || right is not MappingNode second)
        {
            return [];
        }

        Dictionary<string, SpecNode> ours = Index(first);
        Dictionary<string, SpecNode> theirs = Index(second);

        return
        [
            .. ours.Keys.Union(theirs.Keys, StringComparer.Ordinal)
                .Where(key => Differs(key, ours, theirs, fingerprinter, version))
                .OrderBy(key => key, StringComparer.Ordinal),
        ];
    }

    private static bool Differs(
        string key,
        Dictionary<string, SpecNode> ours,
        Dictionary<string, SpecNode> theirs,
        IFingerprinter fingerprinter,
        OpenApiVersion version)
    {
        bool mine = ours.TryGetValue(key, out SpecNode? left);
        bool yours = theirs.TryGetValue(key, out SpecNode? right);

        if (mine != yours)
        {
            return true;
        }

        return !string.Equals(
            fingerprinter.Fingerprint(left!, EquivalenceProfile.Strict, version),
            fingerprinter.Fingerprint(right!, EquivalenceProfile.Strict, version),
            StringComparison.Ordinal);
    }

    private static Dictionary<string, SpecNode> Index(MappingNode mapping) =>
        mapping.Entries.ToDictionary(
            entry => entry.Key.Text, entry => entry.Value, StringComparer.Ordinal);
}
