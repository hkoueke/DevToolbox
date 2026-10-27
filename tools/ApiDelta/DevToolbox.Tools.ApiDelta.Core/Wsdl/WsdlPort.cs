namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un accès : la liaison qu'il emploie, et l'adresse déclarée.</summary>
/// <param name="Name">Son nom.</param>
/// <param name="BindingName">La liaison qu'il emploie.</param>
/// <param name="Location">Où l'accès est déclaré.</param>
/// <remarks>
/// L'adresse déclarée n'est <em>pas</em> portée, et c'est délibéré. Un <c>soap:address</c> porte parfois un
/// identifiant dans son autorité ; un type qui la retiendrait finirait par la journaliser. Les constats
/// qui la concernent nomment l'emplacement, et le lecteur va la lire dans le document.
/// </remarks>
public sealed record WsdlPort(string Name, string BindingName, WsdlLocation Location);
