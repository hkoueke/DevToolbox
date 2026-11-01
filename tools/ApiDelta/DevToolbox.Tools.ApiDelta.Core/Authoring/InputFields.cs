using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Les champs qu'une opération déclare en entrée, tels qu'un placement les voit.</summary>
/// <remarks>
/// Une opération SOAP n'a qu'une entrée : un message, dont la partie désigne un élément global, dont le
/// type porte les champs. Ce sont ces champs que le développeur place — et rien dans le contrat ne dit où
/// chacun doit aller, ce qui est la raison d'être de toute cette fonctionnalité.
/// <para>
/// Un tableau ou un type composé est composite. Les types simples nommés et restreints restent des
/// scalaires : leur déclaration dans le contrat ne les transforme pas en objets.
/// </para>
/// </remarks>
public static class InputFields
{
    /// <summary>Les champs de l'entrée d'une opération.</summary>
    /// <param name="contract">Le contrat lu.</param>
    /// <param name="operation">L'opération.</param>
    /// <returns>Un triplet par champ : son nom, son type déclaré, et s'il est composite.</returns>
    /// <remarks>
    /// Rend une liste vide pour une opération sans entrée, ou dont l'entrée désigne un type que le
    /// contrat ne déclare pas. Une liste vide est une réponse : l'invite saute alors la question du
    /// placement au lieu de présenter une liste vide.
    /// </remarks>
    public static IReadOnlyList<(string Field, string Declared, bool IsComposite)> Of(
        WsdlContract contract, WsdlOperation operation)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Input is not { } input)
        {
            return [];
        }

        List<(string Field, string Declared, bool IsComposite)> fields = [];

        foreach (WsdlPart part in input.Parts)
        {
            fields.AddRange(Particles(contract, part));
        }

        return fields;
    }

    /// <summary>Les éléments du type qu'une partie désigne.</summary>
    private static IEnumerable<(string Field, string Declared, bool IsComposite)> Particles(
        WsdlContract contract, WsdlPart part)
    {
        if (Typed(contract, part) is not { } typeName
            || !contract.Types.TryGetValue(typeName, out XsdType? type))
        {
            return [];
        }

        return type.Particles.Select(particle => (particle.Name, particle.TypeName,
            particle.IsCollection || (contract.Types.TryGetValue(particle.TypeName, out XsdType? declared)
                && declared.Content is not XsdContentKind.Simple)));
    }

    /// <summary>Le type que porte une partie : le sien, ou celui de l'élément qu'elle nomme.</summary>
    /// <remarks>
    /// Les deux formes se rencontrent dans le même contrat. Une partie qui nomme un élément est la forme
    /// « document », celle qu'un service WCF écrit ; une partie qui nomme un type est la forme « RPC ».
    /// </remarks>
    private static string? Typed(WsdlContract contract, WsdlPart part)
    {
        if (part.TypeName is { Length: > 0 } named)
        {
            return named;
        }

        if (part.ElementName is not { Length: > 0 } element)
        {
            return null;
        }

        return contract.Elements is { } elements
            && elements.TryGetValue(element, out string? declared)
            ? declared
            : null;
    }
}
