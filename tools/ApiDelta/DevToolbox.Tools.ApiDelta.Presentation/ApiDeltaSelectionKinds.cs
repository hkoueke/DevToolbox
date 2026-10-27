namespace DevToolbox.Tools.ApiDelta.Presentation;

/// <summary>
/// Les sortes d'éléments qu'apidelta écrit dans un point de reprise.
/// </summary>
/// <remarks>
/// Nommées une seule fois et partagées entre la fabrique qui les écrit et le coordinateur qui les relit :
/// deux chaînes littérales qui doivent correspondre sont une panne de reprise qu'aucun compilateur
/// n'annonce.
/// </remarks>
public static class ApiDeltaSelectionKinds
{
    /// <summary>Le mode de l'exécution.</summary>
    public const string Mode = "ce qui était fait";

    /// <summary>Le fichier en service, par chemin, étiqueté de son empreinte.</summary>
    public const string Production = "fichier en service";

    /// <summary>Le nouveau fichier, par chemin, étiqueté de son empreinte.</summary>
    public const string Candidate = "nouveau fichier";

    /// <summary>Le dossier où les rapports sont écrits.</summary>
    public const string Output = "dossier des rapports";

    /// <summary>Le jeu de règles désigné, s'il y en a un.</summary>
    public const string Ruleset = "règles";

    /// <summary>Une route retenue pour l'extraction.</summary>
    public const string Operation = "route";

    /// <summary>Une route retenue pour entrer en production.</summary>
    public const string CorrectedRoute = "route à ajouter";

    /// <summary>Une différence retenue pour entrer en production.</summary>
    public const string CorrectedDifference = "différence à reporter";

    /// <summary>Un appariement entre une opération du contrat de service et une route.</summary>
    public const string Correspondence = "appariement";
}
