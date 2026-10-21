namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce que la correction de la production a produit, tel que les rapports la lisent.</summary>
/// <param name="Plan">Ce qui a été inséré, élément par élément, et ce qui a été écarté.</param>
/// <param name="Verification">Ce que la vérification a pu établir.</param>
/// <param name="Written">La production corrigée a été écrite.</param>
/// <remarks>
/// Le plan survit même quand rien n'a été écrit, et c'est voulu : un développeur qui a retenu trois
/// éléments et n'en voit aucun dans le résultat doit lire pourquoi au même endroit qu'il aurait lu le
/// succès. Un refus silencieux lui laisserait croire que l'outil a fait le travail.
/// </remarks>
public sealed record CorrectionOutcome(
    CorrectionPlan Plan,
    CorrectionVerification Verification,
    bool Written);
