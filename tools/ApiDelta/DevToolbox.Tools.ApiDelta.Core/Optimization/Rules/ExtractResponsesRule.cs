namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-003 : extrait les réponses répétées à l'identique dans <c>components.responses</c>.
/// </summary>
/// <remarks>
/// Les réponses d'erreur normalisées sont le cas pour lequel cette règle existe : la même 400 recopiée
/// dans chaque opération, à la virgule près. C'est aussi la plus profitable, parce qu'une réponse pèse
/// bien plus qu'une référence.
/// </remarks>
public sealed class ExtractResponsesRule : ExtractionRule
{
    /// <summary>Crée la règle.</summary>
    public ExtractResponsesRule()
        : base("OPT-003")
    {
    }
}
