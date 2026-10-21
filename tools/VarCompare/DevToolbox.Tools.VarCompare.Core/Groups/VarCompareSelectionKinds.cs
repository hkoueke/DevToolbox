namespace DevToolbox.Tools.VarCompare.Core.Groups;

/// <summary>
/// Les sortes d'éléments que varcompare écrit dans un point de reprise.
/// </summary>
/// <remarks>
/// Nommées une seule fois et partagées entre la fabrique qui les écrit et le coordinateur qui les relit :
/// deux chaînes littérales qui doivent correspondre sont une panne de reprise qu'aucun compilateur
/// n'annonce.
/// </remarks>
public static class VarCompareSelectionKinds
{
    /// <summary>La collection lue.</summary>
    public const string Collection = "collection";

    /// <summary>Le projet lu.</summary>
    public const string Project = "projet";

    /// <summary>Un groupe de variables retenu, par identifiant et par nom.</summary>
    public const string Group = "groupe";
}
