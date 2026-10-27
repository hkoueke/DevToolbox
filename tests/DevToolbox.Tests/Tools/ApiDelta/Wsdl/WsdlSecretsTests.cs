using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Un contrat dont l'adresse de service porte un jeton : où ce jeton ressort-il ?
/// </summary>
/// <remarks>
/// Nulle part, et c'est une promesse sur ce que l'outil écrit <em>à propos</em> du contrat — rapports,
/// document engendré, journaux. L'endroit où un secret se trouve en pratique dans un WSDL est la
/// « soap:address » : une adresse de service que quelqu'un a complétée d'une clé pour que l'appel
/// passe, et qui est recopiée de livraison en livraison sans que personne la relise.
/// </remarks>
public sealed class WsdlSecretsTests : IDisposable
{
    /// <summary>Le jeton glissé dans l'adresse du service.</summary>
    private const string Token = "cle-TRES-secrete-a-ne-pas-diffuser-42";

    private static readonly string[] Documents =
        ["catalogue.wsdl", "catalogue-types.xsd", "commun.xsd"];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-wsdl-secret-" + Guid.NewGuid().ToString("N"));

    public WsdlSecretsTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task No_artifact_of_a_wsdl_run_carries_the_address_token()
    {
        WsdlContract contract = await ReadAsync().ConfigureAwait(true);

        contract.Services.Should().NotBeEmpty("le contrat du corpus déclare bien un service");

        SpecReader reader = new(NullLogger<SpecReader>.Instance);
        SpecDocument production = Read(reader, "Wsdl.production.yaml");
        ProjectionResult projected = new Projector().Project(contract, production.DeclaredVersion);

        string markdown = Encoding.UTF8.GetString(
            MarkdownReportWriter.Write(Run(production, projected, contract)).Span);

        string json = Encoding.UTF8.GetString(
            JsonReportWriter.Write(Run(production, projected, contract)).Span);

        foreach ((string what, string written) in new[]
        {
            ("le rapport lisible", markdown),
            ("le rapport machine", json),
            ("le document projeté", Rendered(projected)),
        })
        {
            written.Should().NotContain(Token, "{0} ne doit pas porter le jeton", what);
            written.Should().NotContain(
                "catalogue.entreprise.local",
                "{0} ne doit pas reproduire l'adresse du service", what);
        }
    }

    [Fact]
    public async Task A_port_carries_where_its_address_is_and_never_what_it_says()
    {
        // La garantie est structurelle, et c'est mieux qu'un caviardage : la lecture ne retient pas la
        // valeur de « soap:address », seulement l'endroit où elle se trouve. Un jeton qui s'y cache ne
        // peut donc atteindre aucun artefact, parce qu'il n'entre jamais dans le modèle. Le jour où
        // quelqu'un ajoutera un membre portant l'adresse, cette épreuve tombera — et c'est son objet.
        WsdlContract contract = await ReadAsync().ConfigureAwait(true);

        WsdlPort port = contract.Services[0].Ports[0];

        port.Location.Should().NotBeNull("l'emplacement est ce qui est rapporté");

        typeof(WsdlPort).GetProperties()
            .Select(property => property.PropertyType)
            .Should().NotContain(
                type => type == typeof(Uri),
                "aucun membre de « WsdlPort » ne doit pouvoir porter une adresse");

        port.Name.Should().NotContain(Token);
        port.BindingName.Should().NotContain(Token);
        port.Location.Display.Should().NotContain(Token);

        DevToolbox.Infrastructure.Logging.RedactionRegistry registry = new();

        DevToolbox.Tools.ApiDelta.Infrastructure.ServiceCollectionExtensions
            .RegisterRedactedTypes(registry);

        foreach (Type carrier in new[]
        {
            typeof(WsdlContract), typeof(WsdlService), typeof(WsdlPort), typeof(WsdlDocument),
            typeof(RetrievedOrigin), typeof(ProjectionGap), typeof(ProjectionResult),
        })
        {
            registry.IsForbidden(carrier).Should().BeTrue(
                "« " + carrier.Name + " » ne doit jamais atteindre un puits de journalisation");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Écrit le corpus avec un jeton dans l'adresse du service, puis le lit.</summary>
    private async Task<WsdlContract> ReadAsync()
    {
        foreach (string name in Documents)
        {
            string content = CorpusLoader.Read("Wsdl." + name).Replace(
                "https://catalogue.entreprise.local/services/catalogue",
                "https://catalogue.entreprise.local/services/catalogue?cle=" + Token,
                StringComparison.Ordinal);

            await File.WriteAllTextAsync(
                Path.Combine(_root, name), content, new UTF8Encoding(false)).ConfigureAwait(true);
        }

        WsdlReadResult read = await new WsdlReader().ReadAsync(
            Path.Combine(_root, "catalogue.wsdl"),
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).ConfigureAwait(true);

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(one => one.Message)));

        return read.Contract!;
    }

    private static string Rendered(ProjectionResult projected)
    {
        StringBuilder builder = new();

        Walk(projected.Document.Root, builder);

        return builder.ToString();
    }

    private static void Walk(SpecNode node, StringBuilder builder)
    {
        switch (node)
        {
            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    _ = builder.AppendLine(entry.Key.Text);
                    Walk(entry.Value, builder);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    Walk(item, builder);
                }

                break;

            case ScalarNode scalar:
                _ = builder.AppendLine(scalar.Text);

                break;

            default:
                break;
        }
    }

    private static SpecDocument Read(SpecReader reader, string name)
    {
        SpecReadResult result = reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read(name)),
            name,
            DocumentRole.Production,
            LoadLimits.Default);

        result.Document.Should().NotBeNull();

        return result.Document;
    }

    /// <summary>Une exécution portant tout ce qu'un rapport de contrat sait dire.</summary>
    private static ApiDeltaRun Run(
        SpecDocument production, ProjectionResult projected, WsdlContract contract)
    {
        IReadOnlyList<string> operations = [.. contract.Operations.Select(one => one.Name)];
        SpecDocumentRoutes routes = SpecDocumentRoutes.From(production);

        OperationCorrespondence confirmed = new SimilarityProposer()
            .Propose(operations, routes.Routes)
            .Confirm();

        return new ApiDeltaRun(
            Guid.Empty,
            new RunSelection(RunMode.Correct, "production.yaml", "sans objet", "artefacts"),
            DateTimeOffset.UnixEpoch,
            TimeSpan.Zero,
            production,
            projected.Document,
            [],
            [],
            [],
            Footing: new WsdlFooting(
                DerivationIndex.From(contract, projected),
                confirmed,
                CoverageReport.Establish(
                    confirmed, operations, new HashSet<string>(routes.Routes, StringComparer.Ordinal)),
                projected));
    }
}
