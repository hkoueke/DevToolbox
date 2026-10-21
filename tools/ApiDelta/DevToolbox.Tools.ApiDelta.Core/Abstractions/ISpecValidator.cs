using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Valide la structure d'un document contre la version qu'il déclare.</summary>
/// <remarks>
/// Un port, parce que l'implémentation s'appuie sur une bibliothèque tierce et que rien au-dessus ne doit
/// le savoir : si elle se révèle trop lacunaire ou trop stricte, elle se remplace sans que la comparaison,
/// l'audit ni l'optimisation ne changent d'une ligne.
/// </remarks>
public interface ISpecValidator
{
    /// <summary>Valide le document.</summary>
    /// <param name="document">Le document lu.</param>
    /// <returns>Ce que la validation a trouvé à dire, situé dans l'arbre.</returns>
    IReadOnlyList<Diagnostic> Validate(SpecDocument document);
}
