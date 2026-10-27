namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Pourquoi une construction n'a pas de contrepartie, dit à un lecteur.</summary>
/// <remarks>
/// Écrit une fois, et lu par les trois sorties qui en parlent : le marqueur posé dans le document
/// engendré, la section des limites du rapport lisible, et son équivalent machine. Trois formulations
/// pour le même motif finiraient par divergir, et un lecteur qui comparerait les deux rapports y verrait
/// deux constats là où il n'y en a qu'un.
/// <para>
/// Un motif sans phrase est une erreur de programmation, et non une valeur par défaut : le jeu est clos,
/// et tout membre ajouté à l'énumération doit arriver ici dans le même changement.
/// </para>
/// </remarks>
public static class GapReasons
{
    /// <summary>La raison, en une phrase.</summary>
    /// <param name="reason">Le motif.</param>
    /// <returns>La phrase.</returns>
    public static string Say(ProjectionGapReason reason) => reason switch
    {
        ProjectionGapReason.NumericPrecision =>
            "« totalDigits » et « fractionDigits » n'ont aucun équivalent en JSON Schema.",
        ProjectionGapReason.NoTypedCounterpart =>
            "la forme de ce type survit, son sens ne survit pas : JSON Schema n'a rien qui le dise.",
        ProjectionGapReason.Restriction =>
            "JSON Schema ne sait pas restreindre un schéma référencé.",
        ProjectionGapReason.Attribute =>
            "un attribut XSD n'a pas de contrepartie : JSON ne distingue pas un attribut d'un élément.",
        ProjectionGapReason.SubstitutionGroup =>
            "un groupe de substitution, ou une tête abstraite, n'a pas de contrepartie.",
        ProjectionGapReason.MixedContent =>
            "un contenu mixte — du texte et des éléments au même niveau — n'a pas de contrepartie.",
        ProjectionGapReason.Namespace =>
            "l'espace de noms d'une déclaration ne survit pas : JSON n'en a pas.",
        ProjectionGapReason.Binding =>
            "le style, l'usage et le transport d'une liaison ne survivent pas.",
        ProjectionGapReason.ParticleOrder =>
            "l'ordre des particules d'une séquence est porté par la forme, et non par le contrat.",
        ProjectionGapReason.Xsd11 =>
            "le sens de ce type dépend d'une construction de XSD 1.1 : il est déclaré non projetable "
                + "plutôt que projeté plus permissif qu'il n'est.",
        ProjectionGapReason.ExclusiveByParticle =>
            "un choix XSD est exclusif par particule, « oneOf » par validation.",
        ProjectionGapReason.BaseRelationship =>
            "« allOf » n'exprime pas la relation entre une base et son dérivé.",
        ProjectionGapReason.OpenContent =>
            "« strict », « lax » et « skip » se réduisent à vrai ou faux.",
        ProjectionGapReason.DerivedStatusCode =>
            "le code de statut d'un défaut SOAP est déduit, et non déclaré.",
        ProjectionGapReason.SoapRole =>
            "l'obligation d'un en-tête SOAP survit, son rôle ne survit pas.",
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason), reason, "Ce motif d'écart n'a pas de raison écrite."),
    };
}
