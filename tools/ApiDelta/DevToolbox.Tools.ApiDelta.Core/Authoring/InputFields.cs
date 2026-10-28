using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Les champs qu'une opération déclare en entrée, tels qu'un placement les voit.</summary>
/// <remarks>
/// Une opération SOAP n'a qu'une entrée : un message, dont la partie désigne un élément global, dont le
/// type porte les champs. Ce sont ces champs que le développeur place — et rien dans le contrat ne dit où
/// chacun doit aller, ce qui est la raison d'être de toute cette fonctionnalité.
/// <para>
/// Un champ est dit composite quand son type est déclaré par le contrat. Les scalaires d'un XSD
/// (<c>xs:string</c> et les siens) n'y figurent pas : ils viennent de l'espace de noms du schéma, non du
/// contrat. La distinction est donc une lecture du contrat et non une liste de noms à tenir à jour.
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
            foreach ((string field, string declared) in Particles(contract, part))
            {
                fields.Add((field, declared, contract.Types.ContainsKey(declared)));
            }
        }

        return fields;
    }

    /// <summary>Les éléments du type qu'une partie désigne.</summary>
    private static IEnumerable<(string Field, string Declared)> Particles(
        WsdlContract contract, WsdlPart part)
    {
        if (Typed(contract, part) is not { } typeName
            || !contract.Types.TryGetValue(typeName, out XsdType? type))
        {
            return [];
        }

        return type.Particles.Select(particle => (particle.Name, particle.TypeName));
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
