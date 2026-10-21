using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Deux exécutions complètes du même travail écrivent les mêmes octets, hors du bloc de métadonnées.
/// </summary>
/// <remarks>
/// Le déterminisme n'est pas une élégance : c'est ce qui rend un artefact comparable d'une exécution à la
/// suivante, et donc ce qui permet à une relecture de porter sur ce qui a changé plutôt que sur ce qui a
/// bougé. Le seul écart toléré est le bloc qui porte l'identifiant d'exécution, l'heure et les chemins —
/// tout ce qui varie par nature y est rassemblé, précisément pour que le reste n'ait pas à varier.
/// </remarks>
public sealed class DeterminismTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-determinisme-" + Guid.NewGuid().ToString("N"));

    public DeterminismTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Two_complete_runs_into_separate_directories_differ_only_in_the_metadata_block()
    {
        // Le mode le plus complet qui n'exige pas de candidat : examen, chiffrage, application, extraits
        // d'artefacts. Deux dossiers distincts, pour que rien ne soit partagé entre les deux.
        AuditRunHarness first = new(_root, "premier", apply: true);
        AuditRunHarness second = new(_root, "second", apply: true);

        (await first.RunAsync()).Outcome.Should().Be(RunOutcome.Success);
        (await second.RunAsync()).Outcome.Should().Be(RunOutcome.Success);

        IReadOnlyList<string> names =
        [
            ArtifactNames.DeltaJson,
            ArtifactNames.AuditReport,
            ArtifactNames.OptimizationPlan,
            ArtifactNames.Manifest,
        ];

        foreach (string name in names)
        {
            string one = Normalise(await first.ReadArtifactAsync(name));
            string other = Normalise(await second.ReadArtifactAsync(name));

            other.Should().Be(one, "« {0} » doit être identique hors métadonnées", name);
        }
    }

    [Fact]
    public async Task The_optimized_copy_is_byte_identical_across_runs()
    {
        // La copie optimisée ne porte aucune métadonnée d'exécution : elle doit donc être identique
        // octet pour octet, sans normalisation d'aucune sorte.
        AuditRunHarness first = new(_root, "copie-un", apply: true);
        AuditRunHarness second = new(_root, "copie-deux", apply: true);

        await first.RunAsync();
        await second.RunAsync();

        byte[] one = await File.ReadAllBytesAsync(
            Path.Combine(first.Output, ArtifactNames.OptimizedDocument));
        byte[] other = await File.ReadAllBytesAsync(
            Path.Combine(second.Output, ArtifactNames.OptimizedDocument));

        other.Should().Equal(one);
    }

    [Fact]
    public async Task Everything_that_varies_lives_inside_the_metadata_block()
    {
        // L'invariant qui rend le précédent possible. Si l'identifiant d'exécution apparaissait ailleurs,
        // la normalisation serait un pansement plutôt qu'une conséquence de la conception.
        AuditRunHarness harness = new(_root, "bloc");

        ToolRun run = await harness.RunAsync();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);

        int close = report.IndexOf(
            MarkdownReportWriter.MetadataClose, StringComparison.Ordinal);

        close.Should().BeGreaterThan(0, "le rapport doit porter un bloc de métadonnées");

        report[(close + MarkdownReportWriter.MetadataClose.Length)..]
            .Should().NotContain(
                run.RunId.ToString(),
                "l'identifiant d'exécution ne doit apparaître que dans le bloc");
    }

    [Fact]
    public async Task A_resumed_run_reaches_the_same_artifacts_as_an_uninterrupted_one()
    {
        // Reprendre relit : la sélection seule survit, et le travail est refait sur le contenu actuel.
        // Les artefacts doivent donc être ceux d'une exécution qui n'aurait pas été interrompue.
        AuditRunHarness uninterrupted = new(_root, "entier");
        AuditRunHarness resumed = new(_root, "reprise");

        await uninterrupted.RunAsync();

        // Une première exécution, puis une seconde sur la même sélection et le même dossier : c'est
        // exactement ce qu'une reprise fait, puisqu'elle ne restaure rien d'autre que la sélection.
        await resumed.RunAsync();
        await resumed.RunAsync();

        foreach (string name in new[] { ArtifactNames.DeltaJson, ArtifactNames.AuditReport })
        {
            Normalise(await resumed.ReadArtifactAsync(name))
                .Should().Be(
                    Normalise(await uninterrupted.ReadArtifactAsync(name)),
                    "« {0} » d'une reprise doit valoir celui d'une exécution entière", name);
        }
    }

    /// <summary>
    /// Écarte ce qui varie par nature : le bloc de métadonnées du rapport lisible, son équivalent dans le
    /// rapport lisible par une machine, les empreintes de contenu et les chemins.
    /// </summary>
    /// <remarks>
    /// Deux formes, parce que les deux rapports rassemblent leurs métadonnées différemment : l'un dans un
    /// commentaire délimité, l'autre dans un objet nommé. C'est la même carve-out, écrite deux fois parce
    /// que les deux formats n'ont pas le même moyen de la porter.
    /// </remarks>
    private static string Normalise(string content)
    {
        IReadOnlyList<string> varying =
        [
            "\"runId\"",
            "\"startedAt\"",
            "\"durationMs\"",
            "\"path\"",
            "\"sha256\"",
            "runId: ",
            "path: ",
            "sha256: ",
            "devtoolbox-determinisme-",
        ];

        return string.Join(
            '\n',
            AuditReportTests.Normalise(content)
                .Split('\n')
                .Where(line => !varying.Any(
                    marker => line.Contains(marker, StringComparison.Ordinal))));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
