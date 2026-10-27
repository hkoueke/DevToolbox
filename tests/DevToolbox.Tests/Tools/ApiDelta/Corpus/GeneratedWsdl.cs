using System.Globalization;
using System.Text;

namespace DevToolbox.Tests.Tools.ApiDelta.Corpus;

/// <summary>
/// Engendre les contrats WSDL que personne n'écrirait à la main.
/// </summary>
/// <remarks>
/// Trois formes seulement, et chacune pour une raison : une chaîne d'imports plus longue que le budget,
/// une énumération démesurée, et un type criblé d'attributs. Les embarquer comme fichiers coûterait des
/// mégaoctets dans l'assemblage d'essais pour un contenu entièrement mécanique — c'est le même choix que
/// <see cref="GeneratedDescription"/> avait fait pour le corpus OpenAPI, et pour le même motif.
/// <para>
/// Une chaîne d'imports demande de vrais fichiers, puisque c'est justement la résolution qu'elle éprouve :
/// <see cref="Chain"/> écrit donc sur le disque et rend le chemin d'entrée, là où les deux autres rendent
/// du texte.
/// </para>
/// </remarks>
internal static class GeneratedWsdl
{
    private const string WsdlNamespace = "http://schemas.xmlsoap.org/wsdl/";
    private const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";
    private const string Root = "http://entreprise.local/engendre";

    /// <summary>
    /// Écrit une chaîne d'imports de la profondeur demandée, et rend le document d'entrée.
    /// </summary>
    /// <param name="directory">Le dossier où écrire. Créé s'il n'existe pas.</param>
    /// <param name="depth">Le nombre de schémas de la chaîne.</param>
    /// <returns>Le chemin du WSDL d'entrée.</returns>
    /// <remarks>
    /// Chaque schéma importe le suivant et le dernier n'importe rien. La forme est plausible — une liste
    /// chaînée de types la produit — et c'est exactement sur une forme plausible que l'outil OpenAPI de ce
    /// dépôt avait débordé sa pile. Le lecteur doit épuiser un budget <em>nommé</em>, jamais une pile.
    /// </remarks>
    internal static string Chain(string directory, int depth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);

        _ = Directory.CreateDirectory(directory);

        for (int at = 1; at <= depth; at++)
        {
            File.WriteAllText(
                Path.Combine(directory, Link(at)), Schema(at, depth), new UTF8Encoding(false));
        }

        string entry = Path.Combine(directory, "chain.wsdl");

        File.WriteAllText(entry, Entry(), new UTF8Encoding(false));

        return entry;
    }

    /// <summary>Un schéma portant une énumération démesurée.</summary>
    /// <param name="values">Le nombre de valeurs.</param>
    /// <returns>Le schéma, tel quel.</returns>
    /// <remarks>
    /// Ce n'est pas une curiosité : un service piloté par des listes de codes — pays, devises, natures de
    /// pièce — en porte réellement des dizaines de milliers.
    /// </remarks>
    internal static string EnormousEnumeration(int values)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(values, 1);

        StringBuilder builder = new();

        _ = builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>")
            .Append("<xs:schema xmlns:xs=\"").Append(XsdNamespace).AppendLine("\"")
            .Append("           targetNamespace=\"").Append(Root).AppendLine("/codes\">")
            .AppendLine("  <xs:simpleType name=\"Code\">")
            .AppendLine("    <xs:restriction base=\"xs:string\">");

        for (int at = 0; at < values; at++)
        {
            _ = builder.Append("      <xs:enumeration value=\"C")
                .Append(at.ToString("D6", CultureInfo.InvariantCulture))
                .AppendLine("\"/>");
        }

        return builder.AppendLine("    </xs:restriction>")
            .AppendLine("  </xs:simpleType>")
            .AppendLine("</xs:schema>")
            .ToString();
    }

    /// <summary>Un contrat dont le type de réponse porte un grand nombre d'attributs.</summary>
    /// <param name="attributes">Le nombre d'attributs.</param>
    /// <returns>Le WSDL, schéma compris, en un seul document.</returns>
    /// <remarks>
    /// Un attribut XSD n'a aucun équivalent en OpenAPI. Quatre cents d'entre eux doivent produire
    /// <em>une</em> entrée groupée portant leur nombre, et non quatre cents constats : c'est ce que cette
    /// forme existe pour établir.
    /// </remarks>
    internal static string ManyAttributes(int attributes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attributes, 1);

        StringBuilder builder = new();

        _ = builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>")
            .Append("<wsdl:definitions name=\"Attributs\" targetNamespace=\"").Append(Root)
            .AppendLine("/attributs\"")
            .Append("    xmlns:wsdl=\"").Append(WsdlNamespace).AppendLine("\"")
            .Append("    xmlns:xs=\"").Append(XsdNamespace).AppendLine("\"")
            .Append("    xmlns:tns=\"").Append(Root).AppendLine("/attributs\">")
            .AppendLine("  <wsdl:types>")
            .Append("    <xs:schema targetNamespace=\"").Append(Root)
            .AppendLine("/attributs\" elementFormDefault=\"qualified\">")
            .AppendLine("      <xs:complexType name=\"Charge\">")
            .AppendLine("        <xs:sequence>")
            .AppendLine("          <xs:element name=\"libelle\" type=\"xs:string\"/>")
            .AppendLine("        </xs:sequence>");

        for (int at = 0; at < attributes; at++)
        {
            _ = builder.Append("        <xs:attribute name=\"marque")
                .Append(at.ToString("D4", CultureInfo.InvariantCulture))
                .AppendLine("\" type=\"xs:string\"/>");
        }

        return builder.AppendLine("      </xs:complexType>")
            .AppendLine("      <xs:element name=\"chargeRequete\" type=\"tns:Charge\"/>")
            .AppendLine("      <xs:element name=\"chargeReponse\" type=\"tns:Charge\"/>")
            .AppendLine("    </xs:schema>")
            .AppendLine("  </wsdl:types>")
            .AppendLine("  <wsdl:message name=\"chargeIn\">")
            .AppendLine("    <wsdl:part name=\"corps\" element=\"tns:chargeRequete\"/>")
            .AppendLine("  </wsdl:message>")
            .AppendLine("  <wsdl:message name=\"chargeOut\">")
            .AppendLine("    <wsdl:part name=\"corps\" element=\"tns:chargeReponse\"/>")
            .AppendLine("  </wsdl:message>")
            .AppendLine("  <wsdl:portType name=\"ChargePort\">")
            .AppendLine("    <wsdl:operation name=\"porterCharge\">")
            .AppendLine("      <wsdl:input message=\"tns:chargeIn\"/>")
            .AppendLine("      <wsdl:output message=\"tns:chargeOut\"/>")
            .AppendLine("    </wsdl:operation>")
            .AppendLine("  </wsdl:portType>")
            .AppendLine("</wsdl:definitions>")
            .ToString();
    }

    /// <summary>Le document d'entrée d'une chaîne : il importe le premier maillon.</summary>
    private static string Entry() =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <wsdl:definitions name="Chaine" targetNamespace="{Root}/chaine"
            xmlns:wsdl="{WsdlNamespace}"
            xmlns:xs="{XsdNamespace}">
          <wsdl:types>
            <xs:schema targetNamespace="{Root}/chaine">
              <xs:import namespace="{Root}/maillon/1" schemaLocation="{Link(1)}"/>
            </xs:schema>
          </wsdl:types>
        </wsdl:definitions>
        """;

    /// <summary>Un maillon : il importe le suivant, sauf le dernier.</summary>
    private static string Schema(int at, int depth)
    {
        string next = at < depth
            ? $"""  <xs:import namespace="{Root}/maillon/{at + 1}" schemaLocation="{Link(at + 1)}"/>"""
            : "  <!-- Le dernier maillon n'importe rien. -->";

        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="{XsdNamespace}" targetNamespace="{Root}/maillon/{at}">
        {next}
          <xs:simpleType name="Maillon{at}">
            <xs:restriction base="xs:string"/>
          </xs:simpleType>
        </xs:schema>
        """;
    }

    private static string Link(int at) =>
        "maillon-" + at.ToString("D4", CultureInfo.InvariantCulture) + ".xsd";
}
