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

    /// <summary>
    /// Écrit un contrat à l'échelle d'un gros service, et rend le document d'entrée.
    /// </summary>
    /// <param name="directory">Le dossier où écrire. Créé s'il n'existe pas.</param>
    /// <param name="operations">Le nombre d'opérations du type de port.</param>
    /// <param name="types">Le nombre de types, répartis sur les schémas.</param>
    /// <param name="documents">Le nombre de documents du graphe, WSDL d'entrée compris.</param>
    /// <returns>Le chemin du WSDL d'entrée.</returns>
    /// <remarks>
    /// Large plutôt que profond : les schémas sont importés côte à côte par le document d'entrée, et
    /// aucun n'en importe un autre. C'est la forme qu'ont les gros contrats réels — un schéma par
    /// domaine — et c'est aussi celle qui met la pression là où elle doit l'être : sur le nombre de
    /// documents et de types, non sur la profondeur, que la chaîne éprouve déjà.
    /// <para>
    /// Chaque opération reçoit ses deux messages et ses deux éléments, si bien que la projection a de
    /// quoi produire une charge utile : une mesure faite sur des opérations vides mesurerait le coût de
    /// ne rien faire.
    /// </para>
    /// </remarks>
    internal static string Large(string directory, int operations, int types, int documents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(operations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(types, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(documents, 2);

        _ = Directory.CreateDirectory(directory);

        int schemas = documents - 1;
        int each = (types + schemas - 1) / schemas;

        for (int at = 0; at < schemas; at++)
        {
            File.WriteAllText(
                Path.Combine(directory, "domaine-" + Number(at) + ".xsd"),
                Domain(at, Math.Min(each, types - (at * each)), operations, schemas),
                new UTF8Encoding(false));
        }

        string entry = Path.Combine(directory, "service.wsdl");

        File.WriteAllText(entry, Service(operations, schemas), new UTF8Encoding(false));

        return entry;
    }

    /// <summary>Un schéma de domaine : ses types, et les éléments de message qui lui reviennent.</summary>
    private static string Domain(int index, int types, int operations, int schemas)
    {
        StringBuilder builder = new();

        _ = builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>")
            .Append("<xs:schema xmlns:xs=\"").Append(XsdNamespace).AppendLine("\"")
            .Append("           xmlns:d=\"").Append(Root).Append("/domaine/").Append(Number(index))
            .AppendLine("\"")
            .Append("           targetNamespace=\"").Append(Root).Append("/domaine/")
            .Append(Number(index)).AppendLine("\">");

        for (int at = 0; at < types; at++)
        {
            _ = builder.Append("  <xs:complexType name=\"Type").Append(Number(at)).AppendLine("\">")
                .AppendLine("    <xs:sequence>")
                .AppendLine("      <xs:element name=\"identifiant\" type=\"xs:string\"/>")
                .AppendLine("      <xs:element name=\"libelle\" type=\"xs:string\" minOccurs=\"0\"/>")
                .AppendLine("      <xs:element name=\"quantite\" type=\"xs:int\"/>")
                .AppendLine("    </xs:sequence>")
                .AppendLine("  </xs:complexType>");
        }

        // Les éléments de message de ce domaine : une opération sur « schemas » lui revient.
        for (int at = index; at < operations; at += schemas)
        {
            _ = builder.Append("  <xs:element name=\"operation").Append(Number(at))
                .Append("Requete\" type=\"d:Type").Append(Number(at % Math.Max(types, 1)))
                .AppendLine("\"/>")
                .Append("  <xs:element name=\"operation").Append(Number(at))
                .Append("Reponse\" type=\"d:Type").Append(Number(at % Math.Max(types, 1)))
                .AppendLine("\"/>");
        }

        return builder.AppendLine("</xs:schema>").ToString();
    }

    /// <summary>Le document d'entrée : ses imports, ses messages, son type de port.</summary>
    private static string Service(int operations, int schemas)
    {
        StringBuilder builder = new();

        _ = builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>")
            .Append("<wsdl:definitions xmlns:wsdl=\"").Append(WsdlNamespace).AppendLine("\"")
            .Append("    xmlns:xs=\"").Append(XsdNamespace).AppendLine("\"")
            .Append("    xmlns:tns=\"").Append(Root).AppendLine("/service\"");

        for (int at = 0; at < schemas; at++)
        {
            _ = builder.Append("    xmlns:d").Append(Number(at)).Append("=\"").Append(Root)
                .Append("/domaine/").Append(Number(at)).AppendLine("\"");
        }

        _ = builder.Append("    targetNamespace=\"").Append(Root).AppendLine("/service\">")
            .AppendLine("  <wsdl:types>")
            .Append("    <xs:schema targetNamespace=\"").Append(Root).AppendLine("/service\">");

        for (int at = 0; at < schemas; at++)
        {
            _ = builder.Append("      <xs:import namespace=\"").Append(Root).Append("/domaine/")
                .Append(Number(at)).Append("\" schemaLocation=\"domaine-").Append(Number(at))
                .AppendLine(".xsd\"/>");
        }

        _ = builder.AppendLine("    </xs:schema>").AppendLine("  </wsdl:types>");

        for (int at = 0; at < operations; at++)
        {
            string prefix = "d" + Number(at % schemas);

            _ = builder.Append("  <wsdl:message name=\"operation").Append(Number(at))
                .AppendLine("In\">")
                .Append("    <wsdl:part name=\"corps\" element=\"").Append(prefix)
                .Append(":operation").Append(Number(at)).AppendLine("Requete\"/>")
                .AppendLine("  </wsdl:message>")
                .Append("  <wsdl:message name=\"operation").Append(Number(at)).AppendLine("Out\">")
                .Append("    <wsdl:part name=\"corps\" element=\"").Append(prefix)
                .Append(":operation").Append(Number(at)).AppendLine("Reponse\"/>")
                .AppendLine("  </wsdl:message>");
        }

        _ = builder.AppendLine("  <wsdl:portType name=\"ServicePort\">");

        for (int at = 0; at < operations; at++)
        {
            _ = builder.Append("    <wsdl:operation name=\"lireOperation").Append(Number(at))
                .AppendLine("\">")
                .Append("      <wsdl:input message=\"tns:operation").Append(Number(at))
                .AppendLine("In\"/>")
                .Append("      <wsdl:output message=\"tns:operation").Append(Number(at))
                .AppendLine("Out\"/>")
                .AppendLine("    </wsdl:operation>");
        }

        return builder.AppendLine("  </wsdl:portType>").AppendLine("</wsdl:definitions>").ToString();
    }

    private static string Number(int value) => value.ToString("D4", CultureInfo.InvariantCulture);

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
