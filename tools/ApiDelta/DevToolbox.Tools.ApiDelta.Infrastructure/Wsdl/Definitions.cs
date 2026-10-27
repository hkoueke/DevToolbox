using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Ce qu'un document <c>wsdl:definitions</c> déclare, avant que les schémas ne soient compilés.</summary>
/// <param name="TargetNamespace">Son espace de noms.</param>
/// <param name="Messages">Ses messages.</param>
/// <param name="Operations">Les opérations de ses types de port, non encore liées.</param>
/// <param name="Bindings">Ses liaisons, par nom d'opération.</param>
/// <param name="Services">Ses services.</param>
/// <param name="InlineSchemas">Les schémas écrits dans <c>wsdl:types</c>.</param>
/// <remarks>
/// Une forme intermédiaire, interne à l'infrastructure : les opérations y portent encore des noms de
/// message au lieu des messages, parce que la résolution des deux demande d'avoir lu tout le document.
/// Elle n'atteint jamais le cœur, qui ne connaît que <see cref="WsdlContract"/>.
/// </remarks>
internal sealed record Definitions(
    string? TargetNamespace,
    IReadOnlyList<WsdlMessage> Messages,
    IReadOnlyList<OperationDeclaration> Operations,
    IReadOnlyDictionary<string, WsdlBinding> Bindings,
    IReadOnlyList<WsdlService> Services,
    IReadOnlyList<InlineSchema> InlineSchemas);
