using DevToolbox.Tools.ApiDelta.Core.Operations;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Une différence que le développeur a désignée, une seule, sur une route précise.</summary>
/// <param name="Route">La route dont elle relève.</param>
/// <param name="ChangeId">L'identifiant de la sorte de changement.</param>
/// <param name="At">L'emplacement, sous sa forme textuelle.</param>
/// <remarks>
/// Quatre chaînes, et c'est tout : ni valeur avant, ni valeur après, ni nœud. C'est ce qui permet à cette
/// sélection d'entrer dans un point de reprise sans qu'une valeur de la description puisse s'y glisser,
/// et c'est aussi pourquoi reprendre relit au lieu de restaurer — la différence est retrouvée en
/// recomparant, elle n'est jamais rechargée.
/// <para>
/// Désigner une différence par son emplacement et sa sorte, et non par son rang dans une liste : un rang
/// désignerait autre chose dès qu'une des deux descriptions aurait bougé, et l'outil appliquerait alors
/// avec assurance un changement que personne n'avait retenu.
/// </para>
/// </remarks>
public sealed record DifferenceSelector(OperationSelector Route, string ChangeId, string At)
{
    /// <summary>La forme affichable et mémorisable.</summary>
    public string Display => Route.Display + " · " + ChangeId + " · " + At;

    /// <summary>Relit la forme affichable.</summary>
    /// <param name="text">Le texte, tel que <see cref="Display"/> l'a produit.</param>
    /// <param name="selector">La sélection relue.</param>
    /// <returns><see langword="true"/> si le texte était lisible.</returns>
    public static bool TryParse(string? text, out DifferenceSelector? selector)
    {
        selector = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split(" · ", StringSplitOptions.None);

        if (parts.Length != 3)
        {
            return false;
        }

        if (!OperationSelector.TryParse(parts[0], out OperationSelector? route))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        selector = new DifferenceSelector(route!, parts[1], parts[2]);

        return true;
    }
}
