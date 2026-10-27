using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Correction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Écrit la production consolidée : la production, plus ce que le développeur a retenu.</summary>
public interface ICorrectionBuilder
{
    /// <summary>Établit ce qui serait inséré, sans rien écrire.</summary>
    /// <param name="request">Ce qu'il faut pour corriger.</param>
    /// <returns>Le plan, ou un échec déjà affichable.</returns>
    /// <remarks>
    /// Séparé de la construction pour que l'outil puisse annoncer la liste complète et la faire accepter
    /// avant qu'un octet ne soit écrit. Reconstituer cette liste ailleurs aurait dédoublé les conditions
    /// qui décident de ce qui entre, et les deux copies auraient fini par différer.
    /// </remarks>
    Result<CorrectionPlan> Plan(CorrectionRequest request);

    /// <summary>Construit la production consolidée.</summary>
    /// <param name="request">Ce qu'il faut pour corriger.</param>
    /// <returns>Les octets et ce qu'ils portent, ou un échec déjà affichable.</returns>
    Result<BuiltCorrection> Build(CorrectionRequest request);
}
