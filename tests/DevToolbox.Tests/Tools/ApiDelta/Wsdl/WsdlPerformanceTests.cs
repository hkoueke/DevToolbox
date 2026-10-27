using System.Diagnostics;
using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Un gros contrat se lit-il, se projette-t-il et se compare-t-il en moins de trente secondes ?
/// </summary>
/// <remarks>
/// Mesuré <em>par phase</em>, et c'est la leçon de la tranche précédente : son épreuve de performance
/// avait trouvé un débordement de pile et non un ralentissement, et seule la courbe par phase avait
/// montré où. Un chiffre global dirait « c'est lent » sans dire de quoi, et on optimiserait la phase qui
/// n'était pas en cause.
/// <para>
/// Le budget est large exprès. Ce qui est guetté n'est pas la seconde de trop mais le changement de
/// régime : une lecture quadratique sur le nombre de documents, un parcours récursif qui repart de zéro
/// à chaque type. Une épreuve serrée mesurerait la machine d'intégration continue ; celle-ci mesure la
/// forme de l'algorithme.
/// </para>
/// </remarks>
public sealed class WsdlPerformanceTests : IDisposable
{
    private const int Operations = 200;
    private const int Types = 5_000;
    private const int Documents = 20;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-echelle-" + Guid.NewGuid().ToString("N"));

    private readonly ITestOutputHelper _output;

    public WsdlPerformanceTests(ITestOutputHelper output)
    {
        _output = output;

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Two_hundred_operations_and_five_thousand_types_across_twenty_documents()
    {
        string entry = GeneratedWsdl.Large(_root, Operations, Types, Documents);

        Stopwatch whole = Stopwatch.StartNew();
        Stopwatch phase = Stopwatch.StartNew();

        WsdlReadResult read = await new WsdlReader().ReadAsync(
            entry,
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).ConfigureAwait(true);

        TimeSpan reading = phase.Elapsed;

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Take(3).Select(one => one.Message)));

        WsdlContract contract = read.Contract!;

        contract.Documents.Should().HaveCount(Documents);
        contract.Operations.Should().HaveCount(Operations);
        contract.Types.Should().HaveCountGreaterThanOrEqualTo(Types);

        phase.Restart();

        ProjectionResult projected = new Projector().Project(contract, OpenApiVersion.V30);

        TimeSpan projecting = phase.Elapsed;

        projected.Figures.Operations.Should().Be(Operations);

        phase.Restart();

        SpecDocument production = Read();

        Result<IReadOnlyList<Difference>> compared = new DocumentComparer(new OpenApiMetamodel())
            .Compare(projected.Document, production, CancellationToken.None);

        TimeSpan comparing = phase.Elapsed;

        compared.IsSuccess.Should().BeTrue(compared.Failure?.Message);

        whole.Stop();

        _output.WriteLine(
            "lecture " + Said(reading) + " · projection " + Said(projecting)
            + " · comparaison " + Said(comparing) + " · total " + Said(whole.Elapsed));

        // Les phases sont bornées une par une : un total qui tiendrait grâce à deux phases rapides
        // masquerait celle qui a changé de régime, et c'est elle qu'on cherche.
        reading.Should().BeLessThan(TimeSpan.FromSeconds(20), "lecture : " + Said(reading));
        projecting.Should().BeLessThan(TimeSpan.FromSeconds(10), "projection : " + Said(projecting));
        comparing.Should().BeLessThan(TimeSpan.FromSeconds(10), "comparaison : " + Said(comparing));

        whole.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(30), "l'ensemble : " + Said(whole.Elapsed));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Said(TimeSpan elapsed) =>
        elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s";

    private static SpecDocument Read()
    {
        SpecReadResult result = new SpecReader(NullLogger<SpecReader>.Instance).ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read("Wsdl.production.yaml")),
            "production.yaml",
            DocumentRole.Production,
            LoadLimits.Default);

        result.Document.Should().NotBeNull();

        return result.Document;
    }
}
