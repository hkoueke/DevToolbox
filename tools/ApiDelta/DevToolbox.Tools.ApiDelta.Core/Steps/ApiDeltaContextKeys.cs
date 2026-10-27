namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Les clés sous lesquelles les étapes d'apidelta se transmettent ce qu'elles produisent.
/// </summary>
/// <remarks>
/// Nommées une seule fois : deux chaînes littérales qui doivent correspondre sont une panne qu'aucun
/// compilateur n'annonce. Noter ce qui vivra ici et ne doit jamais atteindre un point de reprise — les
/// documents, les différences, les constats — par opposition à la sélection, qui est faite pour y aller.
/// </remarks>
public static class ApiDeltaContextKeys
{
    /// <summary>La sélection : ce que le développeur a choisi.</summary>
    public const string Selection = "apidelta.selection";

    /// <summary>La sélection restaurée d'une exécution interrompue, le temps d'être consommée.</summary>
    public const string ResumedSelection = "apidelta.resumed-selection";

    /// <summary>Le jeu de règles en vigueur, valeurs livrées comprises.</summary>
    /// <remarks>
    /// Déposé par la sélection, qui est la seule à pouvoir refuser de démarrer. Les étapes qui suivent le
    /// lisent ici plutôt que de retomber sur les valeurs livrées : retomber serait appliquer une
    /// configuration que personne n'a écrite.
    /// </remarks>
    public const string Ruleset = "apidelta.ruleset";

    /// <summary>Les mises en sourdine, déjà validées.</summary>
    public const string Suppressions = "apidelta.suppressions";

    /// <summary>La description de production, telle qu'elle a été lue.</summary>
    public const string ProductionDocument = "apidelta.production";

    /// <summary>La description candidate, telle qu'elle a été lue.</summary>
    public const string CandidateDocument = "apidelta.candidate";

    /// <summary>Ce que la lecture et la validation ont trouvé à dire.</summary>
    public const string Diagnostics = "apidelta.diagnostics";

    /// <summary>Ce qui a changé entre les deux descriptions.</summary>
    public const string Differences = "apidelta.differences";

    /// <summary>La version recommandée, et ce qui l'a commandée.</summary>
    public const string VersionRecommendation = "apidelta.version";

    /// <summary>Le graphe des références de la production, tel que la validation l'a résolu.</summary>
    public const string ReferenceGraph = "apidelta.references";

    /// <summary>Les verdicts de la validation structurelle de la production.</summary>
    /// <remarks>
    /// Distincts des diagnostics de l'exécution : l'audit doit pouvoir dire « la structure a été validée
    /// et n'a rien à signaler », ce qu'une liste vide de diagnostics ne permet pas de distinguer de « la
    /// validation n'a pas eu lieu ».
    /// </remarks>
    public const string StructuralVerdicts = "apidelta.verdicts";

    /// <summary>Ce que l'audit a constaté, et le sort de chaque règle.</summary>
    public const string Audit = "apidelta.audit";

    /// <summary>Ce que le nettoyage rapporterait, chiffré et non appliqué.</summary>
    public const string Plan = "apidelta.plan";

    /// <summary>Les dépendances fermées de ce que le correctif emporte.</summary>
    public const string DependencyClosure = "apidelta.closure";

    /// <summary>Ce qui serait inséré en production, énoncé avant toute écriture.</summary>
    public const string CorrectionPlan = "apidelta.correction.plan";

    /// <summary>La production corrigée, telle qu'elle sera écrite.</summary>
    public const string CorrectedDocument = "apidelta.correction.document";

    /// <summary>Ce que la vérification de la production corrigée a pu établir.</summary>
    public const string CorrectionVerification = "apidelta.correction.verification";

    /// <summary>Ce que l'application du plan a produit, et ce qu'elle a refusé de produire.</summary>
    public const string Optimization = "apidelta.optimization";

    /// <summary>L'extrait construit, émis et vérifié.</summary>
    public const string Extract = "apidelta.extract";

    /// <summary>
    /// L'issue de l'exécution, dans le vocabulaire publié.
    /// </summary>
    /// <remarks>
    /// Déposée pour être affichée. Elle n'est pas le code de sortie du processus, et cette livraison n'en
    /// ajoute aucun : le vocabulaire est fixé maintenant pour qu'une invocation automatisée, le jour où
    /// elle sera spécifiée, ajoute une surface et non un sens.
    /// </remarks>
    public const string Outcome = "apidelta.outcome";

    /// <summary>Le contrat WSDL lu, quand le candidat en est un.</summary>
    public const string WsdlContract = "apidelta.wsdl.contract";

    /// <summary>Ce que la projection du WSDL n'a pas pu porter.</summary>
    public const string WsdlProjectionGaps = "apidelta.wsdl.gaps";

    /// <summary>Le document synthétique projeté depuis le contrat de service.</summary>
    public const string WsdlProjection = "apidelta.wsdl.projection";

    /// <summary>Les routes que la production porte, et la version qu'elle annonce.</summary>
    public const string ProductionRoutes = "apidelta.production.routes";

    /// <summary>L'appariement que le développeur a déclaré dans un fichier.</summary>
    public const string DeclaredCorrespondence = "apidelta.correspondence.declared";

    /// <summary>L'appariement retenu, confirmé ou non.</summary>
    public const string Correspondence = "apidelta.correspondence";

    /// <summary>Ce que la production couvre du contrat de service.</summary>
    public const string Coverage = "apidelta.coverage";

    /// <summary>
    /// Ce sur quoi une sélection faite contre un contrat de service repose.
    /// </summary>
    /// <remarks>
    /// Les trois choses qu'elle rassemble vivent déjà sous leurs propres clés, et l'assemblage est tout
    /// de même déposé : l'écran de sélection, le constructeur de correction et les rapports en ont besoin
    /// ensemble, et chacun les ayant rassemblées à sa façon, ils auraient fini par ne plus répondre la
    /// même chose.
    /// </remarks>
    public const string WsdlFooting = "apidelta.wsdl.footing";

    /// <summary>Les artefacts écrits.</summary>
    public const string Artifacts = "apidelta.artifacts";
}
