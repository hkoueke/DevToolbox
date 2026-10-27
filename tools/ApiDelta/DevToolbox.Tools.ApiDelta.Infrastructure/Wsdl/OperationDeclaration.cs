using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Une opération telle que le type de port l'écrit, avant que ses messages soient résolus.</summary>
/// <param name="Name">Son nom.</param>
/// <param name="Pattern">Son schéma d'échange, déduit de l'ordre des éléments.</param>
/// <param name="InputMessage">Le nom qualifié du message d'entrée, le cas échéant.</param>
/// <param name="OutputMessage">Le nom qualifié du message de sortie, le cas échéant.</param>
/// <param name="FaultMessages">Les noms qualifiés des messages de défaut.</param>
/// <param name="Documentation">Sa documentation, le cas échéant.</param>
/// <param name="Location">Où elle est déclarée.</param>
internal sealed record OperationDeclaration(
    string Name,
    MessagePattern Pattern,
    string? InputMessage,
    string? OutputMessage,
    IReadOnlyList<string> FaultMessages,
    string? Documentation,
    WsdlLocation Location);
