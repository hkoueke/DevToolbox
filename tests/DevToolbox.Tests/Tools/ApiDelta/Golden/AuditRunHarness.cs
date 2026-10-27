using System.Text;
using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Monte la chaîne d'un examen telle que l'exécution la monte : sélection, lecture, validation, examen,
/// chiffrage, écriture.
/// </summary>
/// <remarks>
/// La chaîne entière, et non une étape isolée : ce que ces essais vérifient est ce qu'un développeur
/// obtiendra, artefacts compris. Une étape oubliée ici ferait passer des essais sur une exécution qui
/// n'existe pas.
/// </remarks>
internal sealed class AuditRunHarness
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IApiDeltaPrompt _prompt = Substitute.For<IApiDeltaPrompt>();
    private readonly IReadOnlyList<IOptimizationRule>? _rules;

    /// <summary>Monte le harnais sur une copie d'un fichier du corpus.</summary>
    /// <param name="root">Le dossier de travail de l'essai.</param>
    /// <param name="name">Le nom du sous-dossier, lorsque l'essai en monte plusieurs.</param>
    /// <param name="fileName">Le fichier du corpus à examiner.</param>
    internal AuditRunHarness(
        string root,
        string name = "unique",
        string fileName = "redundant.yaml",
        bool apply = false,
        bool inPlace = false,
        IReadOnlyList<string>? allowed = null,
        IReadOnlyList<IOptimizationRule>? rules = null)
    {
        _rules = rules;
        Output = Path.Combine(root, name, "artefacts");
        ProductionPath = Write(root, name, fileName);

        _clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

        _prompt.AskForSelection(Arg.Any<RunSelection?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RunSelection(
                RunMode.Analyse,
                ProductionPath,
                string.Empty,
                Output,
                ApplySafeOptimizations: apply,
                AllowedReviewRequiredRules: allowed,
                InPlace: inPlace)));

        _prompt.ConfirmCreateDirectory(Arg.Any<string>()).Returns(true);
        _prompt.ConfirmReplace(Arg.Any<IReadOnlyList<string>>()).Returns(true);
        _prompt.ConfirmInPlaceWrite(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    /// <summary>Où les artefacts sont écrits.</summary>
    internal string Output { get; }

    /// <summary>Le fichier examiné.</summary>
    internal string ProductionPath { get; }

    /// <summary>Ce que l'application a produit, une fois l'exécution faite.</summary>
    internal OptimizationOutcome? Optimization { get; private set; }

    /// <summary>Ce que le chiffrage a produit, une fois l'exécution faite.</summary>
    internal OptimizationPlan? Plan { get; private set; }

    /// <summary>Ce que l'examen a constaté, une fois l'exécution faite.</summary>
    internal AuditResult? Findings { get; private set; }

    /// <summary>Déroule l'exécution.</summary>
    /// <param name="cancellationToken">Annule l'exécution.</param>
    /// <returns>L'exécution achevée.</returns>
    internal async Task<ToolRun> RunAsync(CancellationToken cancellationToken = default)
    {
        OpenApiMetamodel metamodel = new();
        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        IReadOnlyList<IToolStep> steps =
        [
            WriteStepFactory.Select(_prompt),
            new ReadTargetStep(reader),
            new ValidateStep(new OpenApiSemanticAdapter(), new ReferenceGraph()),
            new AuditStep(
                new AuditEngine(
                    Audit.AuditHarness.Wired(),
                    metamodel,
                    new Fingerprinter(new Canonicalizer(metamodel))),
                ApiDeltaOptions.Default,
                _clock),
            new PlanStep(ApiDeltaOptions.Default),
            WriteStepFactory.Create(_prompt, _clock, _rules),
        ];

        ToolRun run = new(
            Guid.NewGuid(),
            "apidelta",
            _clock.UtcNow,
            [.. steps.Select(step => step.Name)]);

        IRecoveryPrompt recovery = Substitute.For<IRecoveryPrompt>();
        recovery.AskAsync(Arg.Any<StepState>(), Arg.Any<CancellationToken>())
            .Returns(RecoveryChoice.Abort);

        SequentialStepRunner runner = new(
            _clock,
            Substitute.For<IRunCheckpointStore>(),
            [],
            recovery,
            NullLogger<SequentialStepRunner>.Instance);

        ToolRunContext context = new(run);

        ToolRun finished = await runner.RunAsync(context, steps, cancellationToken);

        Plan = context.Get<OptimizationPlan>(ApiDeltaContextKeys.Plan);
        Findings = context.Get<AuditResult>(ApiDeltaContextKeys.Audit);
        Optimization = context.Get<OptimizationOutcome>(ApiDeltaContextKeys.Optimization);

        return finished;
    }

    /// <summary>Le contenu d'un artefact écrit.</summary>
    /// <param name="fileName">Son nom.</param>
    /// <returns>Le contenu, en texte.</returns>
    internal Task<string> ReadArtifactAsync(string fileName) =>
        File.ReadAllTextAsync(Path.Combine(Output, fileName));

    private static string Write(string root, string name, string fileName)
    {
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, CorpusLoader.Read(fileName), new UTF8Encoding(false));

        return path;
    }
}
