using System.Diagnostics;
using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DevToolbox.Tests.Tools.ApiDelta;

/// <summary>
/// Ce que l'outil tient sous le nombre.
/// </summary>
/// <remarks>
/// La promesse est de trente secondes pour mille opérations et dix mille schémas. Les durées par étape
/// sont consignées plutôt que seulement comparées au total : quand un jour le total sera dépassé, la
/// question sera « laquelle des étapes a changé », et un essai qui ne rend qu'un nombre ne répond pas.
/// </remarks>
public sealed class PerformanceTests : IDisposable
{
    private const int Operations = 1_000;

    private const int Schemas = 10_000;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-tenue-" + Guid.NewGuid().ToString("N"));

    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task A_thousand_operations_and_ten_thousand_schemas_audit_inside_the_budget()
    {
        string path = Path.Combine(_root, "engendre.yaml");

        await File.WriteAllTextAsync(path, GeneratedDescription.Wide(Operations, Schemas));

        long bytes = new FileInfo(path).Length;

        Stopwatch total = Stopwatch.StartNew();

        SpecDocument document = await Timed(
            "lecture",
            async () =>
            {
                SpecReadResult read = await new SpecReader(NullLogger<SpecReader>.Instance)
                    .ReadAsync(path, DocumentRole.Production, Limits(bytes), CancellationToken.None);

                read.IsFaithful.Should().BeTrue(
                    string.Join(" · ", read.Diagnostics.Select(d => d.Message)));

                return read.Document!;
            });

        AuditResult audit = Timed("examen", () => AuditHarness.Audit(document));

        OptimizationPlan plan = Timed(
            "chiffrage",
            () => TransformationPlanner.Plan(document, audit, Ruleset.Default));

        total.Stop();

        _output.WriteLine(
            $"total {total.ElapsedMilliseconds} ms · {bytes} octets · "
            + $"{audit.Findings.Count} constats · {plan.Groups.Count} groupes");

        audit.Findings.Should().NotBeEmpty("une description engendrée répétitive doit donner des constats");

        // Ce corpus porte un anneau de dix mille schémas qui se désignent l'un l'autre, et la traversée
        // des références s'arrête à quatre cents maillons — une borne trouvée en mesurant, parce qu'une
        // chaîne de dix mille faisait déborder la pile. Le graphe est donc tronqué, et les deux règles
        // qui en dépendent le disent au lieu de conclure.
        //
        // Cet essai affirmait l'inverse — « aucune règle ne doit renoncer sous le nombre » — et il
        // passait : la troncature n'était qu'un avertissement, que personne n'écoutait. L'audit se
        // déclarait complet sur une traversée qui avait vu quatre cents maillons sur dix mille. Ce qui
        // est éprouvé ici est la tenue sous le nombre, et la tenue n'a jamais voulu dire l'omniscience.
        audit.IsComplete.Should().BeFalse(
            "la traversée est tronquée par la profondeur, et une analyse incomplète le dit");

        audit.NotEvaluated.Should().NotBeEmpty("au moins une règle dépend du graphe tronqué");

        audit.NotEvaluated.Should().OnlyContain(
            outcome => outcome.Reason != null && outcome.Reason.Length > 0,
            "une règle qui renonce doit dire pourquoi");

        total.Elapsed.Should().BeLessThan(
            Budget,
            "mille opérations et dix mille schémas doivent s'examiner en moins de {0} s",
            Budget.TotalSeconds);
    }

    [Fact]
    public async Task The_audit_walks_the_tree_once_whatever_the_number_of_rules()
    {
        // Trente-cinq règles coûtent une traversée, non trente-cinq. L'essai l'établit par le temps :
        // doubler la taille double le coût, là où une traversée par règle le multiplierait.
        string small = await Written("petit.yaml", GeneratedDescription.Wide(100, 500));
        string large = await Written("grand.yaml", GeneratedDescription.Wide(200, 1_000));

        TimeSpan first = await AuditedAsync(small);
        TimeSpan second = await AuditedAsync(large);

        _output.WriteLine(
            $"100/500 : {first.TotalMilliseconds:F0} ms · 200/1000 : {second.TotalMilliseconds:F0} ms");

        // Large de marge : ce qui est éprouvé est l'absence d'explosion, non un facteur précis.
        second.Should().BeLessThan(
            first * 8 + TimeSpan.FromSeconds(2),
            "le coût doit suivre la taille, non le nombre de règles");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Les limites relevées à la taille de l'essai, qui dépasse le budget livré.</summary>
    private static LoadLimits Limits(long bytes) =>
        LoadLimits.Default with { MaxBytes = Math.Max(bytes * 2, LoadLimits.Default.MaxBytes) };

    private static async Task<TimeSpan> AuditedAsync(string path)
    {
        SpecReadResult read = await new SpecReader(NullLogger<SpecReader>.Instance).ReadAsync(
            path, DocumentRole.Production, Limits(new FileInfo(path).Length), CancellationToken.None);

        Stopwatch watch = Stopwatch.StartNew();

        AuditHarness.Audit(read.Document!);

        watch.Stop();

        return watch.Elapsed;
    }

    private async Task<string> Written(string name, string content)
    {
        string path = Path.Combine(_root, name);
        await File.WriteAllTextAsync(path, content);

        return path;
    }

    /// <summary>Chronomètre une étape et consigne sa durée.</summary>
    private T Timed<T>(string step, Func<T> work)
    {
        Stopwatch watch = Stopwatch.StartNew();
        T result = work();
        watch.Stop();

        _output.WriteLine($"{step} : {watch.ElapsedMilliseconds} ms");

        return result;
    }

    private async Task<T> Timed<T>(string step, Func<Task<T>> work)
    {
        Stopwatch watch = Stopwatch.StartNew();
        T result = await work();
        watch.Stop();

        _output.WriteLine($"{step} : {watch.ElapsedMilliseconds} ms");

        return result;
    }
}
