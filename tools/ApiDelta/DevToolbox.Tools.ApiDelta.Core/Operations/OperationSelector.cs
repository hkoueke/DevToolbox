using System.Globalization;

namespace DevToolbox.Tools.ApiDelta.Core.Operations;

/// <summary>Une opération que le développeur a désignée.</summary>
/// <param name="Path">Le gabarit de chemin, tel que la description l'écrit.</param>
/// <param name="Method">La méthode.</param>
/// <remarks>
/// Deux chaînes, et c'est ce qui compte : une sélection survit à un redémarrage sous cette forme, puis se
/// résout de nouveau contre une description relue. Un rang dans l'arbre ne le permettrait pas, car il
/// désignerait autre chose dès que le document aurait changé.
/// </remarks>
public sealed record OperationSelector(string Path, HttpOperationMethod Method)
{
    /// <summary>La forme affichable et mémorisable : <c>POST /documents</c>.</summary>
    public string Display =>
        Method.ToString().ToUpperInvariant() + " " + Path;

    /// <summary>Relit la forme affichable.</summary>
    /// <param name="text">Le texte, tel que <see cref="Display"/> l'a produit.</param>
    /// <param name="selector">La sélection relue.</param>
    /// <returns><see langword="true"/> si le texte était lisible.</returns>
    public static bool TryParse(string? text, out OperationSelector? selector)
    {
        selector = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        int separator = text.IndexOf(' ', StringComparison.Ordinal);

        if (separator <= 0 || separator == text.Length - 1)
        {
            return false;
        }

        if (!Enum.TryParse(text[..separator], ignoreCase: true, out HttpOperationMethod method))
        {
            return false;
        }

        selector = new OperationSelector(text[(separator + 1)..], method);

        return true;
    }

    /// <summary>Le nom de la méthode tel qu'une description OpenAPI l'écrit, en minuscules.</summary>
    public string MethodKey => Method.ToString().ToLower(CultureInfo.InvariantCulture);
}
