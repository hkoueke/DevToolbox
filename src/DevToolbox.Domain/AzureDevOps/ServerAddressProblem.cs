namespace DevToolbox.Domain.AzureDevOps;

/// <summary>Ce qui empêche une saisie d'être retenue comme adresse de serveur.</summary>
/// <remarks>
/// Une raison, et non un message : la formulation appartient à la couche de présentation, qui seule sait à
/// qui elle s'adresse. C'est la même séparation qu'entre <see cref="Results.FailureReason"/> et le rendu des
/// échecs.
/// </remarks>
public enum ServerAddressProblem
{
    /// <summary>La saisie est utilisable.</summary>
    None = 0,

    /// <summary>Rien n'a été saisi.</summary>
    Empty,

    /// <summary>La saisie ne se lit pas comme une adresse.</summary>
    Malformed,

    /// <summary>Le schéma indiqué n'est ni <c>https</c> ni <c>http</c>.</summary>
    UnsupportedScheme,

    /// <summary>Le schéma est <c>http</c> alors que le HTTP simple n'est pas autorisé.</summary>
    InsecureScheme,

    /// <summary>Aucun nom d'hôte ne ressort de la saisie.</summary>
    MissingHost,

    /// <summary>La saisie porte des identifiants, ce qu'une adresse de serveur ne doit jamais faire.</summary>
    CarriesCredentials,
}
