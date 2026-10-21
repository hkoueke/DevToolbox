namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-004 : extrait les corps de requête répétés à l'identique dans <c>components.requestBodies</c>.
/// </summary>
/// <remarks>
/// Les corps de requête seulement : un type de média répété n'est pas un corps de requête, et le ranger
/// comme tel produirait une description qui ne se relit pas. Le plan ne propose donc jamais cette règle
/// pour un type de média, et le constat qui le signale recommande autre chose — partager le schéma qu'il
/// décrit.
/// </remarks>
public sealed class ExtractRequestBodiesRule : ExtractionRule
{
    /// <summary>Crée la règle.</summary>
    public ExtractRequestBodiesRule()
        : base("OPT-004")
    {
    }
}
