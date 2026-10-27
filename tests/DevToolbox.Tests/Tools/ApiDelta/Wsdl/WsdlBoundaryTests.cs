using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Les deux frontières que la lecture d'un contrat ne doit pas laisser franchir en silence.
/// </summary>
/// <remarks>
/// Toutes deux relevées par une revue de la demande de tirage, et toutes deux réelles. La première rendait
/// le refus de traversée contournable d'un trait d'union ; la seconde laissait lire la moitié d'un contrat
/// en le déclarant lu fidèlement. Ce sont les deux formes que ce lecteur existe précisément pour arrêter.
/// </remarks>
public sealed class WsdlBoundaryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-frontiere-" + Guid.NewGuid().ToString("N"));

    private readonly WsdlReader _reader = new();

    public WsdlBoundaryTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_sibling_folder_whose_name_extends_the_root_is_outside_it()
    {
        // Sous une racine « …\contrats », l'import « ../contrats-secrets/types.xsd » se normalise en
        // « …\contrats-secrets\types.xsd », dont la chaîne commence bel et bien par la racine. Le
        // contrôle par préfixe l'acceptait donc : le refus de traversée tombait sur un trait d'union, et
        // c'est ce refus qui paie l'ouverture par défaut de la résolution locale.
        string confined = Path.Combine(_root, "contrats");
        string sibling = Path.Combine(_root, "contrats-secrets");

        Directory.CreateDirectory(confined);
        Directory.CreateDirectory(sibling);

        await File.WriteAllTextAsync(
            Path.Combine(sibling, "types.xsd"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:secret">
              <xs:element name="secret" type="xs:string"/>
            </xs:schema>
            """,
            new UTF8Encoding(false)).ConfigureAwait(true);

        string entry = Path.Combine(confined, "voisin.wsdl");

        await File.WriteAllTextAsync(
            entry,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:voisin">
              <wsdl:types>
                <xs:schema targetNamespace="urn:voisin">
                  <xs:import namespace="urn:secret" schemaLocation="../contrats-secrets/types.xsd"/>
                </xs:schema>
              </wsdl:types>
            </wsdl:definitions>
            """,
            new UTF8Encoding(false)).ConfigureAwait(true);

        WsdlReadResult read = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(confined),
            CancellationToken.None).ConfigureAwait(true);

        read.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == WsdlDiagnosticCodes.ImportOutsideRoot,
            "un dossier voisin n'est pas à l'intérieur de la racine : " + Said(read));
    }

    [Fact]
    public async Task The_root_itself_is_still_inside_the_root()
    {
        // Le revers : resserrer le contrôle ne doit pas refuser le cas ordinaire, où tout vit dans la
        // racine. Un contrôle trop strict se contourne en élargissant la racine, ce qui est pire.
        await File.WriteAllTextAsync(
            Path.Combine(_root, "types.xsd"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:voisin">
              <xs:element name="chose" type="xs:string"/>
            </xs:schema>
            """,
            new UTF8Encoding(false)).ConfigureAwait(true);

        string entry = Path.Combine(_root, "ordinaire.wsdl");

        await File.WriteAllTextAsync(
            entry,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:voisin">
              <wsdl:types>
                <xs:schema targetNamespace="urn:autre">
                  <xs:import namespace="urn:voisin" schemaLocation="types.xsd"/>
                </xs:schema>
              </wsdl:types>
            </wsdl:definitions>
            """,
            new UTF8Encoding(false)).ConfigureAwait(true);

        WsdlReadResult read = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).ConfigureAwait(true);

        read.Diagnostics.Should().NotContain(
            diagnostic => diagnostic.Code == WsdlDiagnosticCodes.ImportOutsideRoot,
            "un schéma voisin dans la racine est le cas ordinaire : " + Said(read));
    }

    [Fact]
    public async Task A_contract_split_across_several_wsdl_documents_is_refused_by_name()
    {
        // « wsdl:import » répartit les messages, les types de port et les liaisons sur plusieurs
        // documents, là où « xs:import » ne répartit que des schémas. Le suiveur ne descend que dans les
        // schémas : un « wsdl:import » n'était donc ni suivi ni refusé, le contrat était bâti sur le
        // seul document d'entrée, et la lecture se déclarait fidèle. Un contrat amputé de ses opérations
        // importées ferait annoncer absentes des routes que le service expose.
        string entry = Path.Combine(_root, "reparti.wsdl");

        await File.WriteAllTextAsync(
            entry,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:tns="urn:reparti" targetNamespace="urn:reparti">
              <wsdl:import namespace="urn:ailleurs" location="ailleurs.wsdl"/>
              <wsdl:portType name="IciPort">
                <wsdl:operation name="lireIci"/>
              </wsdl:portType>
            </wsdl:definitions>
            """,
            new UTF8Encoding(false)).ConfigureAwait(true);

        WsdlReadResult read = await _reader.ReadAsync(
            entry,
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).ConfigureAwait(true);

        read.IsFaithful.Should().BeFalse(
            "la moitié d'un contrat ne doit pas pouvoir se déclarer lue fidèlement");

        read.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == WsdlDiagnosticCodes.ImportUnresolved
                && diagnostic.Message.Contains("wsdl:import", StringComparison.Ordinal),
            "le refus doit nommer la construction en cause : " + Said(read));

        read.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Message.Contains("urn:ailleurs", StringComparison.Ordinal),
            "et dire quel document manque : " + Said(read));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Said(WsdlReadResult read) =>
        string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message));
}
