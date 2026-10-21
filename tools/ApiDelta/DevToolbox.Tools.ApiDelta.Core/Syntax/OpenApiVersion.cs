namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>La version d'OpenAPI qu'une description déclare.</summary>
/// <remarks>
/// Seules les versions prises en charge figurent ici. Une description qui en déclare une autre est refusée
/// avec la liste de celles qui le sont, plutôt que lue au plus proche.
/// </remarks>
public enum OpenApiVersion
{
    /// <summary>OpenAPI 3.0.x.</summary>
    V30,

    /// <summary>OpenAPI 3.1.x.</summary>
    V31,
}
