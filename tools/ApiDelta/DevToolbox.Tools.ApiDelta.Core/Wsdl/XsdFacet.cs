namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Une facette d'un type simple : ce qu'elle contraint, et à quelle valeur.</summary>
/// <param name="Kind">Le nom de la facette, tel que XSD l'écrit.</param>
/// <param name="Value">La valeur, sous sa forme textuelle.</param>
/// <remarks>
/// La valeur reste textuelle parce que la projection l'écrit telle quelle dans le document engendré :
/// l'interpréter puis la réécrire introduirait une occasion de la changer sans le vouloir.
/// </remarks>
public sealed record XsdFacet(string Kind, string Value);
