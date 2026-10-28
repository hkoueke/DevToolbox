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
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;
using DevToolbox.Tools.ApiDelta.Core.Versioning;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
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
        services.AddSingleton<IDocumentComparer, DocumentComparer>();
        services.AddSingleton<IVersionPolicy, VersionRecommender>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<IApiDeltaPrompt, ApiDeltaPrompt>();
        services.AddSingleton<IWsdlProjector, Projector>();
        services.AddSingleton<ICorrespondenceProposer, SimilarityProposer>();
        services.AddSingleton<CorrespondencePicker>();
        services.AddSingleton<ICorrespondencePrompt, CorrespondenceSelectionPrompt>();
        services.AddSingleton<IRouteAuthoringPrompt, RouteAuthoringPrompt>();
        services.AddSingleton<SelectionSummaryRenderer>();
        services.AddSingleton<DiagnosticsView>();
        services.AddSingleton<ComparisonView>();
        services.AddSingleton<FindingsView>();
        services.AddSingleton<ProposalsView>();
        services.AddSingleton<ApiDeltaResumeCoordinator>();

        services.AddSingleton<IRunCheckpointFactory, ApiDeltaCheckpointFactory>();

        AddCorrection(services);
        AddAuditRules(services);
        AddOptimizationRules(services);

        AddSteps(services);

        services.AddSingleton<ITool>(CreateTool);

        return services;
    }


    /// <summary>
    /// Enregistre ce qui écrit la production consolidée.
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
    /// <summary>Déclare chaque étape par son type concret.</summary>
    private static void AddSteps(IServiceCollection services)
    {
        // Les étapes écrites à ce jour, enregistrées par leur type concret puis assemblées dans l'ordre
        // pour cet outil seul : sous IToolStep, elles tomberaient dans une collection commune à toute la
        // boîte à outils, où chaque outil recevrait les étapes des autres. Les étapes suivantes
        // rejoindront cette liste avec la tranche qui les implémente — une exécution ne doit jamais
        // comporter une étape qui ne fait rien, car l'affichage la donnerait pour accomplie.
        services.AddSingleton<SelectStep>();
        services.AddSingleton<ReadTargetStep>();
        services.AddSingleton<ReadSourceStep>();

        // Enregistrée, mais pas encore dans le fil ci-dessous, et pour la raison écrite juste au-dessus :
        // une exécution ne doit pas comporter une étape qui ne fait rien. Tant que la projection d'un
        // contrat de service vers des routes n'existe pas, lire un WSDL ne produirait rien dont la suite
        // puisse se servir, et l'affichage annoncerait pourtant « Lecture du contrat de service »
        // accomplie sur chaque séance OpenAPI. Elle rejoindra la liste avec la tranche qui la consomme.
        services.AddSingleton<ReadWsdlStep>();

        // Même raison que ci-dessus, et le même rendez-vous : l'appariement ne produit rien tant que la
        // sélection des routes à insérer ne le consomme pas. Les deux étapes entrent dans le fil
        // ensemble, avec la tranche qui s'en sert.
        services.AddSingleton<CorrespondStep>();

        // La composition des routes entre dans le fil en même temps que son enregistrement, et c'est
        // délibérément écrit ici : deux étapes de la tranche précédente ont été écrites, enregistrées et
        // éprouvées une par une tout en restant hors de la chaîne pendant deux versions. Un essai de la
        // racine de composition compare désormais la chaîne construite à l'ordre déclaré.
        services.AddSingleton<AuthorStep>();
        services.AddSingleton<ValidateStep>();
        services.AddSingleton<CompareStep>();
        services.AddSingleton<VersionStep>();
        services.AddSingleton<AuditStep>();
        services.AddSingleton<PlanStep>();
        services.AddSingleton<CorrectStep>();
        services.AddSingleton<WriteStep>();
    }

    private static ApiDeltaTool CreateTool(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IStepRunner>(),
            [
                provider.GetRequiredService<SelectStep>(),
                provider.GetRequiredService<ReadTargetStep>(),

                // La lecture du contrat de service précède celle du compagnon OpenAPI : c'est elle qui
                // reconnaît un WSDL à son contenu, et la lecture OpenAPI ne fait rien si le compagnon en
                // est un. L'appariement suit, parce que c'est lui qui projette le contrat dans la forme
                // que la comparaison sait lire — rien ne se compare avant qu'il soit confirmé.
                provider.GetRequiredService<ReadWsdlStep>(),
                provider.GetRequiredService<ReadSourceStep>(),
                provider.GetRequiredService<CorrespondStep>(),
                provider.GetRequiredService<AuthorStep>(),
                provider.GetRequiredService<ValidateStep>(),
                provider.GetRequiredService<CompareStep>(),
                provider.GetRequiredService<VersionStep>(),
                provider.GetRequiredService<AuditStep>(),
                provider.GetRequiredService<PlanStep>(),
                provider.GetRequiredService<CorrectStep>(),
                provider.GetRequiredService<WriteStep>(),
            ],
            provider.GetRequiredService<ApiDeltaResumeCoordinator>(),
            provider.GetRequiredService<SelectionSummaryRenderer>(),
            provider.GetRequiredService<DiagnosticsView>(),
            provider.GetRequiredService<ComparisonView>(),
            provider.GetRequiredService<FindingsView>(),
            provider.GetRequiredService<ProposalsView>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<Spectre.Console.IAnsiConsole>());
}
