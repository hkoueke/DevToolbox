using DevToolbox.Tools.ApiDelta.Core.Metamodel;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>
/// Jusqu'où deux écritures doivent se ressembler pour qu'on les tienne pour la même chose.
/// </summary>
/// <remarks>
/// Trois lectures d'une seule table — le métamodèle — et non trois canonisations. Un profil ne décrit pas
/// un algorithme, il décrit quelles lignes de la table entrent dans la comparaison.
/// <para>
/// Un seul profil peut qualifier une transformation pour une application automatique, et ce n'est pas un
/// réglage : si la documentation est écartée de la comparaison, alors « identiques » ne veut plus dire
/// « interchangeables », et fusionner deux objets reviendrait à jeter la description de l'un d'eux sans
/// l'avoir dit.
/// </para>
/// </remarks>
public sealed record EquivalenceProfile
{
    private EquivalenceProfile(
        string name,
        bool includesDocumentation,
        bool comparesValues,
        bool qualifiesForAutomaticApplication)
    {
        Name = name;
        IncludesDocumentation = includesDocumentation;
        ComparesValues = comparesValues;
        QualifiesForAutomaticApplication = qualifiesForAutomaticApplication;
    }

    /// <summary>Tout : chaque champ, chaque extension, chaque valeur.</summary>
    public static EquivalenceProfile Strict { get; } = new("strict", true, true, true);

    /// <summary>Le contrat : ce qu'un appelant peut constater, la documentation écartée.</summary>
    public static EquivalenceProfile Contract { get; } = new("contract", false, true, false);

    /// <summary>La forme : la structure seule, pour suggérer et rien de plus.</summary>
    public static EquivalenceProfile Shape { get; } = new("shape", false, false, false);

    /// <summary>Les trois profils, dans l'ordre du plus exigeant au moins exigeant.</summary>
    public static IReadOnlyList<EquivalenceProfile> All { get; } = [Strict, Contract, Shape];

    /// <summary>Le nom du profil, tel qu'il apparaît dans un rapport et dans une configuration.</summary>
    public string Name { get; }

    /// <summary>Indique si les champs de documentation entrent dans la comparaison.</summary>
    public bool IncludesDocumentation { get; }

    /// <summary>Indique si les valeurs des scalaires entrent dans la comparaison.</summary>
    public bool ComparesValues { get; }

    /// <summary>
    /// Indique si une équivalence établie sous ce profil peut rendre une transformation applicable
    /// automatiquement.
    /// </summary>
    public bool QualifiesForAutomaticApplication { get; }

    /// <summary>Indique si un emplacement entre dans la comparaison sous ce profil.</summary>
    /// <param name="entry">Ce que le métamodèle sait de l'emplacement.</param>
    /// <returns><see langword="true"/> si l'emplacement compte.</returns>
    /// <remarks>
    /// Une extension de fournisseur compte sous tous les profils. L'outil ne sait pas ce qu'une passerelle
    /// lit : la tenir pour négligeable serait affirmer ce qu'on ne peut pas établir.
    /// </remarks>
    public bool Includes(MetamodelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return IncludesDocumentation || !entry.IsDocumentation;
    }
}
