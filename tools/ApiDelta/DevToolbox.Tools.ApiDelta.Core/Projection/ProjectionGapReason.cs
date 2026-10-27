namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Pourquoi une construction XSD n'a pas de contrepartie, ou n'en a qu'une approchante.</summary>
/// <remarks>
/// Le motif est ce par quoi les écarts se groupent. Quatre cents attributs non portés doivent donner
/// <em>une</em> entrée portant leur nombre, et non quatre cents constats identiques : ils enseignent la
/// même chose, au prix de quatre cents lignes.
/// </remarks>
public enum ProjectionGapReason
{
    /// <summary>« totalDigits » et « fractionDigits » n'ont aucun équivalent en JSON Schema.</summary>
    NumericPrecision,

    /// <summary>Un type dont la forme survit mais dont le sens ne survit pas.</summary>
    NoTypedCounterpart,

    /// <summary>JSON Schema ne sait pas restreindre un schéma référencé.</summary>
    Restriction,

    /// <summary>Un attribut XSD. La plus grosse source d'écarts en pratique.</summary>
    Attribute,

    /// <summary>Un groupe de substitution, ou une tête abstraite.</summary>
    SubstitutionGroup,

    /// <summary>Un contenu mixte.</summary>
    MixedContent,

    /// <summary>L'espace de noms d'une déclaration. JSON n'en a pas.</summary>
    Namespace,

    /// <summary>Le style, l'usage ou le transport d'une liaison.</summary>
    Binding,

    /// <summary>L'ordre des particules dans une séquence : porté dans la forme, pas dans le contrat.</summary>
    ParticleOrder,

    /// <summary>Une construction de XSD 1.1. Le type est déclaré non projetable, et non projeté à peu près.</summary>
    Xsd11,

    /// <summary>Un choix XSD est exclusif par particule, « oneOf » par validation.</summary>
    ExclusiveByParticle,

    /// <summary>« allOf » n'exprime pas la relation entre une base et son dérivé.</summary>
    BaseRelationship,

    /// <summary>« strict », « lax » et « skip » se réduisent à vrai ou faux.</summary>
    OpenContent,

    /// <summary>Le code de statut d'un défaut SOAP est déduit, non déclaré.</summary>
    DerivedStatusCode,

    /// <summary>L'obligation d'un en-tête SOAP survit, son rôle ne survit pas.</summary>
    SoapRole,
}
