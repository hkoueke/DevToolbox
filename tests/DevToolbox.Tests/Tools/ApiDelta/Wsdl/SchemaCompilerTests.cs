using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Deux schémas peuvent revendiquer le même espace de noms. Que dit l'outil de ce qu'il en a fait ?
/// </summary>
/// <remarks>
/// La fusion est le cas licite et courant : un espace de noms réparti sur plusieurs fichiers. Le conflit
/// est l'autre cas, et il demande de nommer les <em>deux</em> documents — car un message qui n'en nomme
/// qu'un envoie corriger le fichier qui n'a peut-être rien à se reprocher.
/// </remarks>
public sealed class SchemaCompilerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-schemas-" + Guid.NewGuid().ToString("N"));

    private readonly WsdlReader _reader = new();

    public SchemaCompilerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Two_files_sharing_a_namespace_are_merged_into_one_set_of_types()
    {
        await WriteAsync("premier.xsd", Schema("Jeton", "8"));
        await WriteAsync("second.xsd", Schema("Marque", "16"));

        WsdlReadResult result = await ReadAsync("premier.xsd", "second.xsd");

        result.IsFaithful.Should().BeTrue(Said(result));
        result.Contract!.Types.Keys.Should()
            .Contain("http://entreprise.local/partage:Jeton")
            .And.Contain("http://entreprise.local/partage:Marque");
    }

    [Fact]
    public async Task A_conflicting_declaration_is_refused_and_names_both_documents()
    {
        // Le même nom, deux définitions. L'outil ne peut pas choisir, et choisir en silence serait le
        // pire des trois comportements possibles.
        await WriteAsync("premier.xsd", Schema("Jeton", "8"));
        await WriteAsync("second.xsd", Schema("Jeton", "64"));

        WsdlReadResult result = await ReadAsync("premier.xsd", "second.xsd");

        result.IsFaithful.Should().BeFalse("deux définitions d'un même type ne font pas un contrat");

        Diagnostic refusal = result.Diagnostics.Should()
            .ContainSingle(diagnostic => diagnostic.Code == WsdlDiagnosticCodes.SchemaConflict)
            .Subject;

        refusal.Message.Should().Contain("Jeton", "le nom en conflit est dit");
        refusal.Message.Should().Contain("premier.xsd");
        refusal.Message.Should().Contain("second.xsd", "les deux documents, pas seulement le dernier lu");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Schema(string type, string length) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
            targetNamespace="http://entreprise.local/partage">
          <xs:simpleType name="{type}">
            <xs:restriction base="xs:string">
              <xs:maxLength value="{length}"/>
            </xs:restriction>
          </xs:simpleType>
        </xs:schema>
        """;

    private async Task<WsdlReadResult> ReadAsync(params string[] imported)
    {
        string imports = string.Concat(imported.Select(name =>
            $"""      <xs:import namespace="http://entreprise.local/partage" schemaLocation="{name}"/>{'\n'}"""));

        string entry = await WriteAsync("entree.wsdl", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:entree">
              <wsdl:types>
                <xs:schema targetNamespace="urn:entree">
            {imports}    </xs:schema>
              </wsdl:types>
            </wsdl:definitions>
            """);

        return await _reader.ReadAsync(
            entry, WsdlLimits.Default, ImportPolicy.ConfinedTo(_root), CancellationToken.None);
    }

    private async Task<string> WriteAsync(string name, string content)
    {
        string path = Path.Combine(_root, name);

        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));

        return path;
    }

    private static string Said(WsdlReadResult result) =>
        string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
}
