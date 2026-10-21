using DevToolbox.Application.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;
using DevToolbox.Tools.ApiDelta.Core.Audit.Governance;
using DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;
using DevToolbox.Tools.ApiDelta.Core.Audit.Validity;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;
using DevToolbox.Tools.ApiDelta.Core.Versioning;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Presentation.Prompts;
using DevToolbox.Tools.ApiDelta.Presentation.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolbox.Tools.ApiDelta.Presentation;

/// <summary>
/// Enregistre les parties d'apidelta situées au-dessus de l'infrastructure : invites, rendu, étapes et
/// l'outil lui-même.
/// </summary>
/// <remarks>
/// Les adaptateurs de fichiers et d'analyse sont enregistrés à part, par le projet d'infrastructure de
/// l'outil, car cet assemblage ne peut volontairement pas référencer l'infrastructure. La racine de
/// composition appelle les deux.
/// <para>
/// Les étapes sont enregistrées nommément, dans l'ordre, et non découvertes par balayage d'assemblage :
/// une étape écrite mais jamais câblée doit faire échouer le test de la racine de composition, pas
/// disparaître en silence.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>Enregistre les étapes, les invites, le rendu et l'outil apidelta.</summary>
    /// <param name="services">La collection de services.</param>
    /// <returns>La collection de services, pour le chaînage.</returns>
    public static IServiceCollection AddApiDelta(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMetamodel, OpenApiMetamodel>();
        services.AddSingleton<ICanonicalizer, Canonicalizer>();
        services.AddSingleton<IFingerprinter, Fingerprinter>();
        services.AddSingleton<IAuditEngine, AuditEngine>();
        services.AddSingleton<IContractEquivalenceVerifier, ContractEquivalenceVerifier>();
        services.AddSingleton<IOptimizationApplier, OptimizationApplier>();
        services.AddSingleton<IOperationCatalog, OperationCatalog>();
        services.AddSingleton<IExtractBuilder, ExtractBuilder>();
        services.AddSingleton<IExtractVerifier, ExtractVerifier>();
        services.AddSingleton<IDocumentComparer, DocumentComparer>();
        services.AddSingleton<IVersionPolicy, VersionRecommender>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<IApiDeltaPrompt, ApiDeltaPrompt>();
        services.AddSingleton<SelectionSummaryRenderer>();
        services.AddSingleton<DiagnosticsView>();
        services.AddSingleton<ComparisonView>();
        services.AddSingleton<FindingsView>();
        services.AddSingleton<ProposalsView>();
        services.AddSingleton<ExtractView>();
        services.AddSingleton<OperationPicker>();
        services.AddSingleton<IOperationSelectionPrompt, OperationSelectionPrompt>();
        services.AddSingleton<ApiDeltaResumeCoordinator>();

        services.AddSingleton<IRunCheckpointFactory, ApiDeltaCheckpointFactory>();

        AddCorrection(services);
        AddAuditRules(services);
        AddOptimizationRules(services);

        // Les étapes écrites à ce jour, enregistrées par leur type concret puis assemblées dans l'ordre
        // pour cet outil seul : sous IToolStep, elles tomberaient dans une collection commune à toute la
        // boîte à outils, où chaque outil recevrait les étapes des autres. Les étapes suivantes
        // rejoindront cette liste avec la tranche qui les implémente — une exécution ne doit jamais
        // comporter une étape qui ne fait rien, car l'affichage la donnerait pour accomplie.
        services.AddSingleton<SelectStep>();
        services.AddSingleton<ReadTargetStep>();
        services.AddSingleton<ReadSourceStep>();
        services.AddSingleton<ValidateStep>();
        services.AddSingleton<CompareStep>();
        services.AddSingleton<VersionStep>();
        services.AddSingleton<AuditStep>();
        services.AddSingleton<PlanStep>();
        services.AddSingleton<ExtractStep>();
        services.AddSingleton<CorrectStep>();
        services.AddSingleton<WriteStep>();

        services.AddSingleton<ITool>(CreateTool);

        return services;
    }


    /// <summary>
    /// Enregistre ce qui écrit la production corrigée.
    /// </summary>
    /// <remarks>
    /// Groupés, comme les règles le sont déjà : la méthode d'enregistrement a une longueur bornée, et un
    /// groupe dit de surcroît que ces quatre services vont ensemble — le constructeur, le vérificateur,
    /// le sélecteur et l'invite ne servent qu'à ce mode.
    /// </remarks>
    private static void AddCorrection(IServiceCollection services)
    {
        services.AddSingleton<ICorrectionBuilder, CorrectionBuilder>();
        services.AddSingleton<ICorrectionVerifier, CorrectionVerifier>();
        services.AddSingleton<CorrectionPicker>();
        services.AddSingleton<ICorrectionSelectionPrompt, CorrectionSelectionPrompt>();
    }

    /// <summary>
    /// Enregistre chaque règle d'audit, nommément.
    /// </summary>
    /// <remarks>
    /// Nommément, et non par balayage d'assemblage. Une règle écrite mais jamais câblée doit faire
    /// échouer un essai, et non disparaître en silence d'un rapport qui se présentera comme complet : le
    /// catalogue publié annonce trente-cinq règles, et un essai compare cette liste à celui-ci.
    /// </remarks>
    private static void AddAuditRules(IServiceCollection services)
    {
        services.AddSingleton<IAuditRule, InvalidStructureRule>();
        services.AddSingleton<IAuditRule, UnresolvedReferenceRule>();
        services.AddSingleton<IAuditRule, UnusedComponentRule>();
        services.AddSingleton<IAuditRule, PathParameterRule>();
        services.AddSingleton<IAuditRule, OperationIdRule>();
        services.AddSingleton<IAuditRule, ResponseDefinitionRule>();
        services.AddSingleton<IAuditRule, RequestBodyRequirednessRule>();
        services.AddSingleton<IAuditRule, NullabilityVersionRule>();
        services.AddSingleton<IAuditRule, TypeFormatRule>();
        services.AddSingleton<IAuditRule, EmptyFieldRule>();

        services.AddSingleton<IAuditRule, RepeatedParameterRule>();
        services.AddSingleton<IAuditRule, RepeatedHeaderSetRule>();
        services.AddSingleton<IAuditRule, RepeatedResponseRule>();
        services.AddSingleton<IAuditRule, RepeatedRequestBodyRule>();
        services.AddSingleton<IAuditRule, DuplicateSchemaRule>();
        services.AddSingleton<IAuditRule, DuplicateExampleRule>();
        services.AddSingleton<IAuditRule, DuplicateEnumRule>();
        services.AddSingleton<IAuditRule, EquivalentAliasRule>();

        services.AddSingleton<IAuditRule, FormattingRule>();
        services.AddSingleton<IAuditRule, OrderingRule>();
        services.AddSingleton<IAuditRule, ResponseKeyQuotingRule>();
        services.AddSingleton<IAuditRule, HeaderCasingRule>();
        services.AddSingleton<IAuditRule, NamingConventionRule>();
        services.AddSingleton<IAuditRule, DeprecationMetadataRule>();
        services.AddSingleton<IAuditRule, DateFormatRule>();
        services.AddSingleton<IAuditRule, DescriptionQualityRule>();
        services.AddSingleton<IAuditRule, RequiredArrayRule>();
        services.AddSingleton<IAuditRule, AdditionalPropertiesRule>();
        services.AddSingleton<IAuditRule, ExtensionStructureRule>();

        services.AddSingleton<IAuditRule, MissingStandardsRule>();
        services.AddSingleton<IAuditRule, InconsistentHeaderRequirednessRule>();
        services.AddSingleton<IAuditRule, SecurityOverrideRule>();
        services.AddSingleton<IAuditRule, PlaceholderRule>();
        services.AddSingleton<IAuditRule, VendorConfigurationRule>();
        services.AddSingleton<IAuditRule, DeadMetadataRule>();
        services.AddSingleton<IAuditRule, SecretShapedValueRule>();
    }

    /// <summary>
    /// Enregistre chaque règle d'optimisation, nommément.
    /// </summary>
    /// <remarks>
    /// Nommément, comme les règles d'audit, et pour une raison plus forte encore : une règle
    /// d'optimisation écrite mais non câblée ferait écarter à l'exécution une transformation que le plan
    /// propose, et le développeur lirait une proposition que rien ne sait appliquer. Le refus est
    /// consigné — l'applicateur dit « aucune règle enregistrée ne sait appliquer OPT-00x » — mais il vaut
    /// mieux qu'un essai le dise avant lui.
    /// </remarks>
    private static void AddOptimizationRules(IServiceCollection services)
    {
        services.AddSingleton<IOptimizationRule, ExtractParametersRule>();
        services.AddSingleton<IOptimizationRule, ExtractResponsesRule>();
        services.AddSingleton<IOptimizationRule, ExtractRequestBodiesRule>();
        services.AddSingleton<IOptimizationRule, ExtractHeadersAndExamplesRule>();
        services.AddSingleton<IOptimizationRule, ExtractInlineSchemasRule>();
        services.AddSingleton<IOptimizationRule, RemoveUnusedComponentsRule>();
        services.AddSingleton<IOptimizationRule, HoistPathParametersRule>();
        services.AddSingleton<IOptimizationRule, AddDeprecationFlagRule>();
    }

    /// <summary>Assemble l'outil avec ses étapes, dans l'ordre d'exécution.</summary>
    private static ApiDeltaTool CreateTool(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IStepRunner>(),
            [
                provider.GetRequiredService<SelectStep>(),
                provider.GetRequiredService<ReadTargetStep>(),
                provider.GetRequiredService<ReadSourceStep>(),
                provider.GetRequiredService<ValidateStep>(),
                provider.GetRequiredService<CompareStep>(),
                provider.GetRequiredService<VersionStep>(),
                provider.GetRequiredService<AuditStep>(),
                provider.GetRequiredService<PlanStep>(),
                provider.GetRequiredService<ExtractStep>(),
                provider.GetRequiredService<CorrectStep>(),
                provider.GetRequiredService<WriteStep>(),
            ],
            provider.GetRequiredService<ApiDeltaResumeCoordinator>(),
            provider.GetRequiredService<SelectionSummaryRenderer>(),
            provider.GetRequiredService<DiagnosticsView>(),
            provider.GetRequiredService<ComparisonView>(),
            provider.GetRequiredService<FindingsView>(),
            provider.GetRequiredService<ProposalsView>(),
            provider.GetRequiredService<ExtractView>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<Spectre.Console.IAnsiConsole>());
}
