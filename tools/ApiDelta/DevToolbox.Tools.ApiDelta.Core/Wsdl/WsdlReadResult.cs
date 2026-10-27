using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Ce qu'une lecture de contrat a produit : le contrat, ou de quoi expliquer son absence.</summary>
/// <param name="Contract">Le contrat, quand il a été lu fidèlement.</param>
/// <param name="Diagnostics">Ce que la lecture a trouvé à dire.</param>
/// <remarks>
/// La même forme que le résultat de lecture d'une description OpenAPI, et pour la même raison : un
/// contrat n'existe que s'il a été lu fidèlement, si bien qu'aucune étape ultérieure ne peut recevoir un
/// contrat à moitié lu. Une erreur de syntaxe, une définition de type de document, un import non résolu ou
/// une version non prise en charge produisent des diagnostics et aucun contrat.
/// </remarks>
public sealed record WsdlReadResult(WsdlContract? Contract, IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Indique que la lecture a abouti.</summary>
    public bool IsFaithful => Contract is not null;

    /// <summary>Un refus, sans contrat.</summary>
    /// <param name="diagnostics">Ce qui l'explique.</param>
    /// <returns>Le résultat.</returns>
    public static WsdlReadResult Refused(IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new WsdlReadResult(null, diagnostics);
    }
}
