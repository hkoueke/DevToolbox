using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Relie les messages d'un graphe WSDL par namespace, sans repli inter-namespace.</summary>
internal static class WsdlGraphBinding
{
    internal static IReadOnlyList<WsdlOperation> Bind(
        IReadOnlyList<Definitions> documents, List<Diagnostic> diagnostics)
    {
        Dictionary<string, WsdlMessage> messages = new(StringComparer.Ordinal);
        foreach (Definitions document in documents)
        {
            foreach (WsdlMessage message in document.Messages)
            {
                string key = Key(document.TargetNamespace, message.Name);
                if (!messages.TryAdd(key, message))
                {
                    Refuse(diagnostics, "Message WSDL déclaré plusieurs fois : " + key);
                }
            }
        }

        List<WsdlOperation> operations = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (Definitions document in documents)
        {
            foreach (OperationDeclaration declaration in document.Operations)
            {
                if (!names.Add(declaration.Name))
                {
                    Refuse(diagnostics, "Nom d'opération WSDL ambigu entre documents : " + declaration.Name);
                    continue;
                }

                WsdlBinding? binding = Binding(documents, declaration, diagnostics);
                WsdlMessage? input = Resolve(messages, document, declaration.InputMessage, diagnostics);
                WsdlMessage? output = Resolve(messages, document, declaration.OutputMessage, diagnostics);
                List<WsdlMessage> faults = [];
                foreach (string fault in declaration.FaultMessages)
                {
                    if (Resolve(messages, document, fault, diagnostics) is { } resolved)
                    {
                        faults.Add(resolved);
                    }
                }

                operations.Add(new WsdlOperation(
                    declaration.Name, declaration.Pattern, input, output,
                    faults, binding, declaration.Documentation, declaration.Location));
            }
        }

        return operations;
    }

    private static WsdlBinding? Binding(
        IReadOnlyList<Definitions> documents, OperationDeclaration operation,
        List<Diagnostic> diagnostics)
    {
        List<WsdlBinding> matches = [];
        foreach (Definitions document in documents)
        {
            if (document.QualifiedBindings is null || document.BindingPortTypes is null)
            {
                continue;
            }

            foreach (KeyValuePair<string, WsdlBinding> entry in document.QualifiedBindings)
            {
                if (!entry.Key.EndsWith(":" + operation.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!document.BindingPortTypes.TryGetValue(entry.Key, out string? portType))
                {
                    Refuse(diagnostics, "Le binding SOAP ne déclare pas de portType : " + entry.Key);
                    continue;
                }

                if (string.Equals(portType, operation.PortType, StringComparison.Ordinal))
                {
                    matches.Add(entry.Value);
                }
            }
        }

        if (matches.Count > 1)
        {
            Refuse(diagnostics, "Plusieurs bindings SOAP correspondent à l'opération : " + operation.Name);
            return null;
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private static WsdlMessage? Resolve(
        Dictionary<string, WsdlMessage> messages, Definitions document,
        string? reference, List<Diagnostic> diagnostics)
    {
        if (reference is null)
        {
            return null;
        }

        string key = reference.Contains(':')
            ? reference : Key(document.TargetNamespace, reference);
        if (messages.TryGetValue(key, out WsdlMessage? message))
        {
            return message;
        }

        Refuse(diagnostics, "Référence de message WSDL introuvable : " + key);
        return null;
    }

    private static string Key(string? space, string local) =>
        string.IsNullOrEmpty(space) ? local : space + ":" + local;

    private static void Refuse(List<Diagnostic> diagnostics, string message) =>
        diagnostics.Add(new Diagnostic(
            WsdlDiagnosticCodes.StructuralViolation, DiagnosticSeverity.Error, message));
}
