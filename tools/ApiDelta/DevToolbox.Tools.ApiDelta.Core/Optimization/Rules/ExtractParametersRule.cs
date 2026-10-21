namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-002 : extrait les paramètres répétés à l'identique dans <c>components.parameters</c>.
/// </summary>
/// <remarks>
/// Le comportement de surcharge est préservé parce que l'extraction ne déplace rien : chaque occurrence
/// reste là où elle était, sous la forme d'une référence. Un paramètre déclaré au niveau de l'opération
/// continue donc de masquer celui du chemin, exactement comme avant. C'est la remontée au niveau du
/// chemin — OPT-008 — qui touche à la portée, et c'est pourquoi elle est une autre règle.
/// </remarks>
public sealed class ExtractParametersRule : ExtractionRule
{
    /// <summary>Crée la règle.</summary>
    public ExtractParametersRule()
        : base("OPT-002")
    {
    }
}
