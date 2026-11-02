using DevToolbox.Tools.ApiDelta.Core.Operations;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>La proposition de placement que le verbe permet de faire, et rien de plus.</summary>
/// <remarks>
/// Une proposition, jamais une décision : le développeur la change champ par champ. Elle existe parce
/// qu'une opération de vingt champs serait inutilisable sans elle, et elle est montrée parce qu'un défaut
/// appliqué sans être montré cacherait une décision.
/// <para>
/// Un champ composite sous un verbe de lecture arrive à <see cref="ParameterLocation.Nowhere" /> et
/// signalé comme demandant une décision. C'est le cas qui compte : le pré-régler au corps reviendrait à
/// écrire un « GET » à corps, et le pré-régler à la requête serait impossible — accepter les défauts en
/// bloc ne doit pas pouvoir écarter un champ sans que personne l'ait voulu.
/// </para>
/// </remarks>
public static class PlacementDefaults
{
    /// <summary>Propose un placement pour chaque champ.</summary>
    /// <param name="method">Le verbe que le développeur a arrêté.</param>
    /// <param name="fields">Les champs de l'entrée : nom, type déclaré, composite ou non.</param>
    /// <returns>Un placement pré-réglé par champ, dans l'ordre du contrat.</returns>
    public static IReadOnlyList<FieldPlacement> For(
        HttpOperationMethod method,
        IReadOnlyList<(string Field, string Declared, bool IsComposite)> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        List<FieldPlacement> placements = [];

        foreach ((string field, string declared, bool composite) in fields)
        {
            placements.Add(new FieldPlacement(
                field, declared, composite, Proposed(method, composite), Required: false));
        }

        return placements;
    }

    /// <summary>Indique qu'un placement proposé attend une décision plutôt qu'un accord.</summary>
    /// <param name="method">Le verbe arrêté.</param>
    /// <param name="placement">Le placement proposé.</param>
    /// <returns><see langword="true" /> quand l'outil n'a pas su proposer.</returns>
    /// <remarks>
    /// Distinct d'un placement « nulle part » voulu : l'un est une décision du développeur, l'autre est
    /// l'aveu que l'outil n'en a pas. Les confondre laisserait un champ disparaître dans un accord global.
    /// </remarks>
    public static bool AwaitsDecision(HttpOperationMethod method, FieldPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        return placement.IsComposite && Reads(method);
    }

    /// <summary>La destination proposée pour un champ.</summary>
    private static ParameterLocation Proposed(HttpOperationMethod method, bool composite)
    {
        if (!Reads(method))
        {
            return ParameterLocation.Body;
        }

        return composite ? ParameterLocation.Nowhere : ParameterLocation.Query;
    }

    /// <summary>Indique qu'un verbe lit, plutôt qu'il écrit.</summary>
    /// <remarks>
    /// « GET » et « DELETE » n'emportent pas de corps en pratique, quoi qu'OpenAPI autorise. Les ranger
    /// ensemble ici est ce qui permet de ne décider du placement qu'une fois.
    /// </remarks>
    private static bool Reads(HttpOperationMethod method) =>
        method is HttpOperationMethod.Get or HttpOperationMethod.Delete;
}
