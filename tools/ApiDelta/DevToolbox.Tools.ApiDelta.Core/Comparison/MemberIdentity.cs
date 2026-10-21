using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>
/// Reconnaît un membre de collection d'un document à l'autre.
/// </summary>
/// <remarks>
/// C'est ce qui distingue « ce paramètre est devenu obligatoire » de « un paramètre a disparu et un autre
/// est apparu ». Un rang ne fait pas une identité : réordonner des paramètres ne les remplace pas, et une
/// comparaison qui les apparierait par position produirait, sur une simple réorganisation, une liste de
/// changements cassants entièrement imaginaire.
/// </remarks>
internal static class MemberIdentity
{
    internal static string Of(
        SpecNode member,
        IdentityRule rule,
        int index,
        IMetamodel metamodel,
        OpenApiVersion version) => rule switch
    {
        IdentityRule.ByNameAndLocation => NameAndLocation(member, index, metamodel, version),
        IdentityRule.ByValue => CanonicalForm.Of(member, metamodel, version),
        _ => index.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// L'identité d'un paramètre : le couple nom et emplacement, ou la référence qui le désigne.
    /// </summary>
    /// <remarks>
    /// Un paramètre écrit par référence n'a ni nom ni emplacement sur place : sa référence <em>est</em> son
    /// identité, et c'est d'ailleurs ce qui permet de voir qu'un paramètre partagé a été remplacé par une
    /// copie littérale.
    /// </remarks>
    private static string NameAndLocation(
        SpecNode member,
        int index,
        IMetamodel metamodel,
        OpenApiVersion version)
    {
        if (member is not MappingNode mapping)
        {
            return index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (mapping.Find("$ref") is ScalarNode reference)
        {
            return "$ref:" + reference.Text;
        }

        string name = (mapping.Find("name") as ScalarNode)?.Text ?? string.Empty;
        string location = (mapping.Find("in") as ScalarNode)?.Text ?? string.Empty;

        return name.Length == 0 && location.Length == 0
            ? CanonicalForm.Of(member, metamodel, version)
            : location + ":" + name;
    }
}
