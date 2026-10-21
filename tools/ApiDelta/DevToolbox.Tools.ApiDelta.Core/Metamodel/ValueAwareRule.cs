namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Les champs dont l'effet dépend de la valeur, et non du seul fait qu'ils ont changé.
/// </summary>
/// <remarks>
/// Pour l'essentiel des champs, la nature du changement suffit : une description modifiée est de la
/// documentation, une réponse supprimée est cassante. Pour ceux-ci, non : passer <c>required</c> de
/// <c>false</c> à <c>true</c> est cassant, l'inverse ne l'est pas ; abaisser <c>maxLength</c> resserre,
/// l'augmenter élargit. La table nomme donc la règle, et le classificateur la connaît.
/// </remarks>
public enum ValueAwareRule
{
    /// <summary>La nature du changement suffit.</summary>
    None,

    /// <summary>Un drapeau d'obligation : le resserrement est l'ajout de l'obligation.</summary>
    RequirednessFlag,

    /// <summary>
    /// Un drapeau de permission : le resserrement est le <em>retrait</em> de la permission.
    /// </summary>
    /// <remarks>
    /// La polarité inverse de <see cref="RequirednessFlag"/>, et c'est exactement pourquoi elle a sa
    /// propre règle. <c>nullable: true</c> élargit — un appelant qui envoyait une absence de valeur est
    /// désormais accepté — là où <c>required: true</c> resserre. Les faire partager une règle revenait à
    /// annoncer cassant un élargissement, ce qui est la façon la plus sûre d'apprendre à une équipe à ne
    /// plus lire l'outil.
    /// </remarks>
    PermissionFlag,

    /// <summary>Une liste de noms obligatoires : s'allonger resserre.</summary>
    RequiredNames,

    /// <summary>Un ensemble de valeurs admises : se raccourcir resserre.</summary>
    EnumMembers,

    /// <summary>Une borne supérieure : baisser resserre.</summary>
    UpperBound,

    /// <summary>Une borne inférieure : monter resserre.</summary>
    LowerBound,

    /// <summary>Un nom de type : en changer rompt, sauf élargissement démontrable.</summary>
    TypeName,

    /// <summary>Un nom de format : en changer rompt les clients engendrés.</summary>
    FormatName,

    /// <summary>Une exigence de sécurité : s'ajouter ou se durcir rompt.</summary>
    SecurityRequirement,

    /// <summary>L'emplacement d'un paramètre : en changer rompt.</summary>
    ParameterLocation,
}
