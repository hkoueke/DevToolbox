using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Presentation.Prompts;
using FluentAssertions;
using NSubstitute;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class AuthoredPipelineTests
{
    [Theory]
    [InlineData(HttpOperationMethod.Post, "/Documents/Custom", false)]
    [InlineData(HttpOperationMethod.Put, "/documents", false)]
    [InlineData(HttpOperationMethod.Post, "/Documents/Custom", true)]
    [InlineData(HttpOperationMethod.Put, "/documents", true)]
    public async Task Customizing_an_existing_derived_endpoint_creates_the_chosen_route_and_leaves_production_intact(
        HttpOperationMethod method, string path, bool placeFields)
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();
        run.Route("deposerDocument").Display.Should().Be("POST /documents");
        AuthoredRoute route = Route("deposerDocument", path, method) with
        {
            Placements = placeFields ? Fields(run, "deposerDocument", false) : [],
        };
        ToolRunContext context = await Author(run, new AuthoringRecord([route]));
        SpecDocument candidate = context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument)!;
        PointerNavigator.Find(candidate.Root, "/paths/~1documents/post").Should().BeNull();
        PointerNavigator.Find(candidate.Root, At(route.Selector).Json).Should().NotBeNull();
        PointerNavigator.Find(run.Candidate.Root, "/paths/~1documents/post").Should().NotBeNull("la projection source reste intacte");

        var compared = await new CompareStep(new DocumentComparer(new OpenApiMetamodel())).ExecuteAsync(context, CancellationToken.None);
        compared.IsSuccess.Should().BeTrue(compared.Failure?.Message);
        IReadOnlyList<Difference> differences = context.Get<IReadOnlyList<Difference>>(ApiDeltaContextKeys.Differences)!;
        var catalogue = CorrectionCatalog.Build(run.Production, candidate, differences, new OperationCatalog(),
            context.Get<WsdlFooting>(ApiDeltaContextKeys.WsdlFooting));
        CorrectionRoute offered = catalogue.Single(item => item.Selector == route.Selector);
        offered.State.Should().Be(RouteState.Absent);
        using TestConsole console = new();
        console.Interactive();
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);
        CorrectionSelection selection = new CorrectionPicker(console).Ask([offered]);
        selection.Routes.Should().Equal(route.Selector);
        selection.Differences.Should().BeEmpty();

        BuiltCorrection built = Build(run, context, selection, differences);
        SpecDocument corrected = run.Reread(built.Serialized);
        SpecNode before = PointerNavigator.Find(run.Production.Root, "/paths/~1documents/post")!;
        SpecNode after = PointerNavigator.Find(corrected.Root, "/paths/~1documents/post")!;
        // L’étendue YAML relue peut inclure l’indentation du nouveau membre suivant.
        Bytes(corrected, after).Should().StartWith(Bytes(run.Production, before));
        PointerNavigator.Find(corrected.Root, At(route.Selector).Json).Should().NotBeNull();
        PointerNavigator.Find(corrected.Root, At(route.Selector).Child("operationId").Json).Should().BeNull();
        (after as MappingNode)!.Find("operationId").Should().BeOfType<ScalarNode>().Which.Text.Should().Be("deposerDocument");
        built.Marks.Should().Contain(mark => mark.Derived != null && string.Equals(mark.Derived.Subject, "deposerDocument", StringComparison.Ordinal));
        var verification = run.Verify(built);
        verification.Established.Should().BeTrue(verification.Explanation);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Multiple_authored_methods_share_one_path_item(bool existingPath, bool anchored)
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Delivered("placements.wsdl", "placements.wsdl");
        string path = existingPath ? "/documents" : "/widgets";
        InsertionPoint insertion = anchored ? InsertionPoint.After("/documents") : InsertionPoint.AtEnd;
        AuthoringRecord record = new([
            Route("creerClient", path, HttpOperationMethod.Get) with { Insertion = insertion, Placements = Fields(run, "creerClient", true) },
            Route("modifierClient", path, HttpOperationMethod.Put) with { Insertion = insertion, Placements = Fields(run, "modifierClient", false) },
        ]);
        ToolRunContext context = await Author(run, record);
        CorrectionSelection selection = new([.. record.Routes.Select(route => route.Selector)]);
        BuiltCorrection built = Build(run, context, selection, []);
        SpecDocument corrected = run.Reread(built.Serialized);
        MappingNode paths = PointerNavigator.Find(corrected.Root, "/paths").Should().BeOfType<MappingNode>().Subject;
        paths.Entries.Count(entry => string.Equals(entry.Key.Text, path, StringComparison.Ordinal)).Should().Be(1);
        MappingNode item = paths.Find(path).Should().BeOfType<MappingNode>().Subject;
        item.Find("get").Should().NotBeNull();
        item.Find("put").Should().NotBeNull();
        item.Entries.Should().HaveCount(existingPath ? 3 : 2);
        (item.Find("get") as MappingNode)!.Find("operationId").Should().BeOfType<ScalarNode>().Which.Text.Should().Be("creerClient");
        (item.Find("put") as MappingNode)!.Find("operationId").Should().BeOfType<ScalarNode>().Which.Text.Should().Be("modifierClient");
        var verification = run.Verify(built);
        verification.Established.Should().BeTrue(verification.Explanation);
    }

    [Fact]
    public async Task Choice_branches_remain_in_the_serialized_body_and_the_original_projection()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();
        FieldPlacement[] fields = [.. InputFields.Of(run.Contract,
            run.Contract.Operations.Single(operation => string.Equals(operation.Name, "lireDocument", StringComparison.Ordinal)))
            .Select(field => new FieldPlacement(field.Field, field.Declared, field.IsComposite, ParameterLocation.Body, false))];
        AuthoringRecord record = new([Route("lireDocument", "/Document/Search", HttpOperationMethod.Post) with { Placements = fields }]);
        ToolRunContext context = await Author(run, record);
        BuiltCorrection built = Build(run, context, new CorrectionSelection([record.Routes[0].Selector]), []);
        SpecNode root = run.Reread(built.Serialized).Root;
        const string Body = "/paths/~1Document~1Search/post/requestBody";
        var branches = PointerNavigator.Find(root, Body + "/content/application~1json/schema/oneOf")
            .Should().BeOfType<SequenceNode>().Subject;
        branches.Items.Should().HaveCount(3);
        foreach (MappingNode branch in branches.Items.Cast<MappingNode>())
        {
            branch.Find("required").Should().BeOfType<SequenceNode>().Which.Items.Should().ContainSingle();
        }

        var verification = run.Verify(built);
        verification.Established.Should().BeTrue(verification.Explanation);
    }

    [Theory]
    [InlineData(ParameterLocation.Query)]
    [InlineData(ParameterLocation.Header)]
    [InlineData(ParameterLocation.Nowhere)]
    public async Task Incompatible_choice_placements_fail_before_comparison_without_replacing_the_candidate(ParameterLocation location)
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();
        var fields = InputFields.Of(run.Contract,
            run.Contract.Operations.Single(operation => string.Equals(operation.Name, "lireDocument", StringComparison.Ordinal)));
        FieldPlacement[] placed = [.. fields.Select((field, index) => new FieldPlacement(field.Field, field.Declared,
            field.IsComposite, index == 0 ? location : ParameterLocation.Body, false))];
        AuthoringRecord record = new([Route("lireDocument", "/Search", HttpOperationMethod.Post) with { Placements = placed }]);
        var (step, context) = Setup(run, record);
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Failure!.Message.Should().Contain("composition").And.Contain("corps");
        context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument).Should().BeSameAs(run.Candidate);
    }

    private static async Task<ToolRunContext> Author(WsdlCorrectionRun run, AuthoringRecord record)
    {
        var (step, context) = Setup(run, record);
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Failure?.Message);
        var first = context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument)!;
        var repeated = await step.ExecuteAsync(context, CancellationToken.None);
        repeated.IsSuccess.Should().BeTrue(repeated.Failure?.Message);
        var second = context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument)!;
        new CanonicalEmitter().Emit(first.Root, CanonicalFormatting.Default).ToArray()
            .Should().Equal(new CanonicalEmitter().Emit(second.Root, CanonicalFormatting.Default).ToArray());
        return context;
    }

    private static (AuthorStep Step, ToolRunContext Context) Setup(WsdlCorrectionRun run, AuthoringRecord record)
    {
        IRouteAuthoringPrompt prompt = Substitute.For<IRouteAuthoringPrompt>();
        prompt.AuthorAsync(Arg.Any<IReadOnlyList<AuthoringSubject>>(), Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<OfferedHeader>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(record));
        ToolRunContext context = new(new ToolRun(Guid.NewGuid(), "apidelta", DateTimeOffset.UtcNow, [ApiDeltaStepNames.Author, ApiDeltaStepNames.Compare]));
        context.Set(ApiDeltaContextKeys.WsdlContract, run.Contract);
        context.Set(ApiDeltaContextKeys.WsdlProjection, run.Projection);
        context.Set(ApiDeltaContextKeys.CandidateDocument, run.Candidate);
        context.Set(ApiDeltaContextKeys.ProductionDocument, run.Production);
        context.Set(ApiDeltaContextKeys.WsdlFooting, run.Footing);
        return (new AuthorStep(prompt), context);
    }

    private static BuiltCorrection Build(WsdlCorrectionRun run, ToolRunContext context, CorrectionSelection selection,
        IReadOnlyList<Difference> differences)
    {
        SpecDocument candidate = context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument)!;
        var closure = new DependencyResolver(new OpenApiMetamodel()).Close([.. selection.Routes.Select(At)],
            candidate, run.Production, CancellationToken.None);
        closure.IsSuccess.Should().BeTrue(closure.Failure?.Message);
        var request = new CorrectionRequest(run.Production, candidate, differences, selection, closure.Value, null,
            CanonicalFormatting.Default, false, context.Get<WsdlFooting>(ApiDeltaContextKeys.WsdlFooting),
            context.Get<AuthoringRecord>(ApiDeltaContextKeys.Authoring));
        var built = new CorrectionBuilder(new OperationCatalog(), new SplicingEmitter(new CanonicalEmitter())).Build(request);
        built.IsSuccess.Should().BeTrue(built.Failure?.Message);
        return built.Value;
    }

    private static FieldPlacement[] Fields(WsdlCorrectionRun run, string name, bool read) =>
        [.. InputFields.Of(run.Contract, run.Contract.Operations.Single(operation => string.Equals(operation.Name, name, StringComparison.Ordinal)))
            .Select(field => new FieldPlacement(field.Field, field.Declared, field.IsComposite,
                (read, field.IsComposite) switch
                {
                    (false, _) => ParameterLocation.Body,
                    (true, true) => ParameterLocation.Nowhere,
                    _ => ParameterLocation.Query,
                }, false))];

    private static AuthoredRoute Route(string name, string path, HttpOperationMethod method) =>
        new(name, path, method, [], [], InsertionPoint.AtEnd, RouteDerivationKind.Derived, false);

    private static SpecPointer At(OperationSelector route) => SpecPointer.Root.Child("paths").Child(route.Path).Child(route.MethodKey);

    private static byte[] Bytes(SpecDocument document, SpecNode node) =>
        document.Source.Slice((int)node.Span.Start, (int)node.Span.Length).ToArray();
}
