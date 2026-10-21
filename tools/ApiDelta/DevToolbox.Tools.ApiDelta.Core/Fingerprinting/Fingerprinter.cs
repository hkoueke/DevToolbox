using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>
/// Donne à un sous-arbre une empreinte stable : SHA-256 de sa forme canonique.
/// </summary>
/// <remarks>
/// Le choix d'une empreinte cryptographique n'est pas une précaution de principe. Regrouper sur
/// l'empreinte seule, sans repasser comparer les membres, revient à affirmer « ce sont les mêmes » — et
/// c'est précisément cette affirmation qui rend une transformation applicable automatiquement. Une
/// fonction de hachage ordinaire obligerait à une passe de confirmation, ou mentirait un jour sur
/// l'essentiel.
/// <para>
/// Le résultat est mis en cache par nœud et par profil : l'audit empreinte chaque candidat une fois, et
/// plusieurs règles interrogent les mêmes. Le cache est attaché aux nœuds eux-mêmes, si bien qu'il
/// disparaît avec le document : un cache qui survit à ce qu'il décrit est une fuite, et cet outil est
/// fait pour des descriptions de quinze mégaoctets.
/// </para>
/// </remarks>
public sealed class Fingerprinter : IFingerprinter
{
    private readonly ConditionalWeakTable<SpecNode, Dictionary<string, string>> _cache = [];
    private readonly ICanonicalizer _canonicalizer;

    /// <summary>Crée l'empreinteur.</summary>
    /// <param name="canonicalizer">Produit la forme sur laquelle l'empreinte est prise.</param>
    public Fingerprinter(ICanonicalizer canonicalizer)
    {
        ArgumentNullException.ThrowIfNull(canonicalizer);
        _canonicalizer = canonicalizer;
    }

    /// <inheritdoc />
    public string Fingerprint(SpecNode node, EquivalenceProfile profile, OpenApiVersion version)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(profile);

        Dictionary<string, string> byProfile = _cache.GetOrCreateValue(node);

        if (byProfile.TryGetValue(profile.Name, out string? cached))
        {
            return cached;
        }

        ReadOnlyMemory<byte> canonical = _canonicalizer.Canonicalize(node, profile, version);
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(canonical.Span));

        byProfile[profile.Name] = fingerprint;

        return fingerprint;
    }
}
