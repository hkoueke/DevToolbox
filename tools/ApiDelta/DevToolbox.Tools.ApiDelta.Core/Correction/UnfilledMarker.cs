using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Un endroit que la projection n'a pas pu remplir, et que l'outil refuse de deviner.</summary>
/// <param name="Target">L'objet qui porte le marqueur.</param>
/// <param name="Construct">Ce qui manque, tel que XSD l'écrit.</param>
/// <param name="Subject">L'opération ou le type concerné.</param>
/// <param name="Why">Pourquoi cela n'a pas de contrepartie.</param>
/// <remarks>
/// Un schéma plausible serait la seule sortie dont un lecteur ne peut pas récupérer le fait que personne
/// ne sait. C'est pour cela que le marqueur existe : il n'est pas une politesse envers le relecteur, il
/// est la seule forme honnête de ce que la projection n'a pas su faire.
/// <para>
/// Posé sur un objet, comme la trace d'origine, et pour la même raison : OpenAPI n'autorise les membres
/// <c>x-</c> que là. Le marqueur nomme donc la construction au lieu de s'y accrocher.
/// </para>
/// </remarks>
public sealed record UnfilledMarker(
    SpecPointer Target, string Construct, string Subject, string Why)
{
    /// <summary>Le nom du membre d'extension qui porte les marqueurs d'un objet.</summary>
    public const string Member = "x-apidelta-unfilled";

    /// <summary>Le nom du membre qui, au niveau du document, en déclare le nombre.</summary>
    /// <remarks>
    /// Au niveau du document, et pas seulement sur les objets concernés : un document inachevé doit être
    /// reconnaissable comme inachevé depuis lui seul. Un relecteur qui ouvre le fichier par le haut
    /// apprend qu'il y a sept endroits non remplis avant d'avoir à les chercher.
    /// </remarks>
    public const string Count = "marqueursNonRemplis";

    /// <summary>Ce que le marqueur dit, en une ligne.</summary>
    public string Display => Construct + " (" + Subject + ") — " + Why;
}
