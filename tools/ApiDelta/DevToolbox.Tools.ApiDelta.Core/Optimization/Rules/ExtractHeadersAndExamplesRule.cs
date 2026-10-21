namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-005 : extrait les en-têtes de réponse et les exemples répétés à l'identique.
/// </summary>
/// <remarks>
/// Deux familles pour une règle, parce que le remède est le même et que la décision l'est aussi : l'un et
/// l'autre sont des objets recopiés qu'aucun appelant ne distingue de leur référence. La famille de
/// destination vient de l'emplacement de l'occurrence, si bien qu'un en-tête va dans
/// <c>components.headers</c> et un exemple dans <c>components.examples</c> sans que la règle ait à le
/// savoir.
/// </remarks>
public sealed class ExtractHeadersAndExamplesRule : ExtractionRule
{
    /// <summary>Crée la règle.</summary>
    public ExtractHeadersAndExamplesRule()
        : base("OPT-005")
    {
    }
}
