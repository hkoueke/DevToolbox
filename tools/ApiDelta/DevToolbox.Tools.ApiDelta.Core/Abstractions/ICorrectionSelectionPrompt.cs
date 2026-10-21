using DevToolbox.Tools.ApiDelta.Core.Correction;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Demande ce qui doit entrer en production, la comparaison une fois faite.</summary>
/// <remarks>
/// Un port distinct de l'invite de sélection, et pour une raison de séquence : la liste vient de ce que la
/// comparaison a trouvé, et la comparaison n'a lieu qu'après que les deux fichiers ont été lus. Poser la
/// question plus tôt obligerait à faire saisir des chemins à la main — et surtout à choisir sans voir
/// l'effet que la comparaison a établi, ce qui est la seule chose qu'elle existe pour produire.
/// </remarks>
public interface ICorrectionSelectionPrompt
{
    /// <summary>Demande ce qu'il faut porter en production.</summary>
    /// <param name="routes">Les routes du candidat, celles qui n'apportent rien comprises.</param>
    /// <returns>Ce que le développeur retient, vide s'il renonce.</returns>
    CorrectionSelection Ask(IReadOnlyList<CorrectionRoute> routes);

    /// <summary>Énonce ce qui va être inséré, et demande l'accord.</summary>
    /// <param name="plan">Ce qui serait inséré, élément par élément.</param>
    /// <returns><see langword="true"/> si le développeur accepte.</returns>
    /// <remarks>
    /// L'énoncé vient <em>avant</em> l'écriture et il est ce que le développeur accepte. C'est la seule
    /// invite de cet outil dont la réponse engage un document qu'un relecteur lira ensuite comme s'il
    /// avait été écrit à la main.
    /// </remarks>
    bool Confirm(CorrectionPlan plan);
}
