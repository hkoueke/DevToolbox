using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Un endroit que la projection n'a pas pu remplir, et que l'outil refuse de deviner.</summary>
/// <param name="Target">L'objet concerné.</param>
/// <param name="Construct">Ce qui manque, tel que XSD l'écrit.</param>
/// <param name="Subject">L'opération ou le type concerné.</param>
/// <param name="Why">Pourquoi cela n'a pas de contrepartie.</param>
/// <remarks>
/// Un schéma plausible serait la seule sortie dont un lecteur ne peut pas récupérer le fait que personne
/// ne sait. C'est pour cela que ceci existe : ce n'est pas une politesse envers le relecteur, c'est la
/// seule forme honnête de ce que la projection n'a pas su faire.
/// <para>
/// Ces endroits étaient inscrits dans le document produit, sous un membre <c>x-apidelta-unfilled</c>,
/// avec leur nombre déclaré à la racine. Ils ne le sont plus : le fichier écrit est destiné à devenir un
/// contrat publié et ne porte que de l'OpenAPI. Le rapport est désormais le seul endroit qui les nomme,
/// ce qui lui donne une responsabilité qu'il n'avait pas — d'où l'essai qui compare son compte à celui
/// du plan.
/// </para>
/// </remarks>
public sealed record UnfilledMarker(
    SpecPointer Target, string Construct, string Subject, string Why)
{
    /// <summary>Ce que le marqueur dit, en une ligne.</summary>
    public string Display => Construct + " (" + Subject + ") — " + Why;
}
