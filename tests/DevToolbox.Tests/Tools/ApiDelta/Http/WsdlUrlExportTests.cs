using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Http;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tools.ApiDelta.Presentation;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Http;

public sealed class WsdlUrlExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wsdl-url-" + Guid.NewGuid().ToString("N"));
    private readonly Routes _routes = new();
    private readonly HttpClient _client;
    private readonly WsdlOpenApiExporter _exporter;
    private string Snapshot => Path.Combine(_root, "capture");

    public WsdlUrlExportTests()
    {
        Directory.CreateDirectory(_root);
        _client = new HttpClient(_routes);
        _exporter = new WsdlOpenApiExporter(new ContractFetcher(_client,
            new AddressGuard((host, _) => Task.FromResult<IPAddress[]>(
                [IPAddress.Parse(string.Equals(host, "internal.test", StringComparison.Ordinal) ? "169.254.169.254" : "8.8.8.8")])),
            new WsdlReader(), new Clock()));
        _routes.Graph();
    }

    [Fact]
    public async Task URL_graph_with_imported_bindings_same_name_schemas_and_include_is_replayable_without_network()
    {
        string yaml = await Export();
        yaml.Should().Be(CorpusLoader.Read("Wsdl.url.expected.openapi.yaml"));
        yaml.Should().Contain("text/xml").And.Contain("application/soap+xml").And.Contain("Fault")
            .And.Contain("'200'").And.Contain("'202'").And.NotContain("private.example").And.NotContain("contracts.test");
        Directory.GetFiles(Path.Combine(Snapshot, "sources")).Should().HaveCount(6);
        string manifestText = await File.ReadAllTextAsync(Path.Combine(Snapshot, "manifest.json"));
        manifestText.Should().NotContain("contracts.test").And.NotContain("private.example");
        JsonNode proof = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Snapshot, "compatibility.json")))!;
        proof["YamlSha256"]!.GetValue<string>().Should().Be(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(yaml))));
        proof["SparkyImport"]!.GetValue<string>().Should().StartWith("Not tested");
        int requests = _routes.Requests.Count;

        string replay = await new WsdlOpenApiExporter().ExportAsync(Path.Combine(Snapshot, "manifest.json"),
            "SOAP acceptance", "1.0", CancellationToken.None);

        replay.Should().Be(yaml);
        _routes.Requests.Should().HaveCount(requests);
        _routes.Requests.Should().OnlyContain(request => request.Method == HttpMethod.Get
            && request.RequestUri!.Host == "contracts.test" && request.Headers.Authorization == null);
        _routes.Pinned.Should().OnlyContain(address => address == "8.8.8.8");
    }

    [Fact]
    public async Task Redirected_entry_and_import_keep_the_final_base_URI_and_original_alias()
    {
        _routes.Items["/start"] = new("", Location: "/v2/service.wsdl");
        _routes.Graph("/v2");
        _routes.Items["/v2/bindings/current.wsdl"] = _routes.Items["/v2/bindings/shared.wsdl"];
        _routes.Items["/v2/bindings/shared.wsdl"] = new("", Location: "current.wsdl");

        string yaml = await Export("https://contracts.test/start");

        yaml.Should().Contain("openapi: 3.0.3");
        _routes.Requests.Should().Contain(request => request.RequestUri!.AbsolutePath == "/v2/schema/a/extra.xsd");
    }

    [Theory]
    [InlineData("https://user:password@contracts.test/service.wsdl")]
    [InlineData("https://contracts.test/service.wsdl?token=secret")]
    [InlineData("https://contracts.test/service.wsdl#secret")]
    [InlineData("http://contracts.test/service.wsdl")]
    [InlineData("ftp://contracts.test/service.wsdl")]
    [InlineData("https://2130706433/service.wsdl")]
    [InlineData("https://[::ffff:127.0.0.1]/service.wsdl")]
    public async Task Unsafe_entry_is_refused_before_any_request_and_leaves_no_collection(string source)
    {
        Func<Task> export = async () => _ = await Export(source);

        var failure = await export.Should().ThrowAsync<WsdlExportException>();
        failure.Which.Message.Should().NotContain("password").And.NotContain("secret").And.NotContain("contracts.test");
        _routes.Requests.Should().BeEmpty();
        AssertNoCollection();
    }

    [Theory]
    [InlineData("https://other.test/bindings/shared.wsdl", false)]
    [InlineData("https://internal.test/bindings/shared.wsdl", true)]
    [InlineData("file:///outside/secret.wsdl", false)]
    [InlineData("https://contracts.test/bindings/shared.wsdl?token=secret", false)]
    public async Task Imports_cannot_expand_the_authorized_network_scope(string location, bool authorizeHost)
    {
        _routes.EntryImport(location);
        WsdlUrlExportOptions options = new(Snapshot,
            AdditionalHosts: authorizeHost ? ["internal.test"] : null);
        Func<Task> export = async () => _ = await Export(options: options);

        var failure = await export.Should().ThrowAsync<WsdlExportException>();
        failure.Which.Message.Should().NotContain("secret").And.NotContain("internal.test");
        _routes.Requests.Should().ContainSingle();
        AssertNoCollection();
    }

    [Fact]
    public async Task An_explicitly_allowed_public_import_host_is_usable()
    {
        _routes.EntryImport("https://cdn.test/bindings/shared.wsdl");

        string yaml = await Export(options: new(Snapshot, AdditionalHosts: ["cdn.test"]));

        yaml.Should().Contain("openapi: 3.0.3");
        _routes.Requests.Should().Contain(request => request.RequestUri!.Host == "cdn.test");
    }

    [Theory]
    [InlineData("https://internal.test/service.wsdl", true)]
    [InlineData("https://other.test/service.wsdl", false)]
    [InlineData("http://contracts.test/service.wsdl", false)]
    public async Task Redirects_are_rechecked_before_the_next_request(string target, bool authorizeHost)
    {
        _routes.Items["/service.wsdl"] = new("", Location: target);
        Func<Task> export = async () => _ = await Export(options: new(Snapshot,
            AdditionalHosts: authorizeHost ? ["internal.test"] : null));

        await export.Should().ThrowAsync<WsdlExportException>();
        _routes.Requests.Should().ContainSingle();
        AssertNoCollection();
    }

    [Fact]
    public async Task A_redirect_loop_is_bounded()
    {
        _routes.Items["/service.wsdl"] = new("", Location: "/service.wsdl");
        Func<Task> export = async () => _ = await Export();

        await export.Should().ThrowAsync<WsdlExportException>().WithMessage("*COLLECTION_REDIRECTREFUSED*");
        _routes.Requests.Should().HaveCount(6);
        AssertNoCollection();
    }

    [Theory]
    [InlineData("document-count")]
    [InlineData("import-depth")]
    [InlineData("response-bytes")]
    [InlineData("document-bytes")]
    [InlineData("graph-bytes")]
    [InlineData("xml-depth")]
    public async Task Each_collection_budget_is_enforced_and_failure_is_atomic(string budget)
    {
        WsdlLimits limits = budget switch
        {
            "document-count" => WsdlLimits.Default with { MaxDocumentCount = 1 },
            "import-depth" => WsdlLimits.Default with { MaxImportChainDepth = 0 },
            "response-bytes" => WsdlLimits.Default with { MaxResponseBytes = 10 },
            "document-bytes" => WsdlLimits.Default with { MaxDocumentBytes = 10 },
            "graph-bytes" => WsdlLimits.Default with { MaxGraphBytes = Encoding.UTF8.GetByteCount(_routes.Items["/service.wsdl"].Body) + 1 },
            _ => WsdlLimits.Default with { MaxNestingDepth = 1 },
        };
        Func<Task> export = async () => _ = await Export(options: new(Snapshot, Limits: limits));

        await export.Should().ThrowAsync<WsdlExportException>();
        AssertNoCollection();
    }

    [Fact]
    public async Task Document_discovery_is_bounded_before_requesting_all_designated_imports()
    {
        string imports = string.Concat(Enumerable.Range(0, 1000).Select(index =>
            "<import namespace='urn:other' location='import-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".wsdl'/>"));
        _routes.Items["/service.wsdl"] = new("<definitions xmlns='http://schemas.xmlsoap.org/wsdl/'>" + imports + "</definitions>");
        Func<Task> export = async () => _ = await Export(options: new(Snapshot,
            Limits: WsdlLimits.Default with { MaxDocumentCount = 2 }));

        await export.Should().ThrowAsync<WsdlExportException>();
        _routes.Requests.Should().ContainSingle();
        AssertNoCollection();
    }

    [Fact]
    public async Task Collection_limits_cannot_exceed_the_offline_replay_safety_caps()
    {
        Func<Task> export = async () => _ = await Export(options: new(Snapshot,
            Limits: WsdlLimits.Default with { MaxGraphBytes = long.MaxValue }));

        await export.Should().ThrowAsync<WsdlExportException>().WithMessage("*COLLECTION_LIMITS_INVALID*");
        _routes.Requests.Should().BeEmpty();
        AssertNoCollection();
    }

    [Fact]
    public async Task Reduced_projection_budgets_are_applied_after_collection()
    {
        string schema = _routes.Items["/schema/a/extra.xsd"].Body
            .Replace("<xs:schema ", "<xs:schema xmlns:tns='urn:types:a' ", StringComparison.Ordinal)
            .Replace("type=\"xs:int\"", "type='tns:Small'", StringComparison.Ordinal)
            .Replace("</xs:schema>", "<xs:simpleType name='Small'><xs:restriction base='xs:string'><xs:enumeration value='A'/><xs:enumeration value='B'/></xs:restriction></xs:simpleType></xs:schema>", StringComparison.Ordinal);
        _routes.Items["/schema/a/extra.xsd"] = new(schema);
        Func<Task> export = async () => _ = await Export(options: new(Snapshot,
            Limits: WsdlLimits.Default with { MaxEnumerationValues = 1 }));

        await export.Should().ThrowAsync<WsdlExportException>();
        _routes.Requests.Should().HaveCount(6);
        AssertNoCollection();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_oversized_content_length_does_not_cause_a_full_body_read(bool announced)
    {
        using CountingBody body = new(new byte[100_000]);
        _routes.Body = body;
        _routes.DeclaredLength = announced ? 100_000 : null;
        Func<Task> export = async () => _ = await Export(options: new(Snapshot,
            Limits: WsdlLimits.Default with { MaxResponseBytes = 16 }));

        await export.Should().ThrowAsync<WsdlExportException>();
        body.Received.Should().Be(announced ? 0 : 17);
        AssertNoCollection();
    }

    [Theory]
    [InlineData("<!DOCTYPE definitions [<!ENTITY e SYSTEM 'file:///secret'>]><definitions>&e;</definitions>")]
    [InlineData("<html>password=secret</html>")]
    public async Task Invalid_or_hostile_XML_is_refused_without_publishing_response_content(string content)
    {
        _routes.Items["/service.wsdl"] = new(content);
        Func<Task> export = async () => _ = await Export();

        var failure = await export.Should().ThrowAsync<WsdlExportException>();
        failure.Which.Message.Should().NotContain("secret").And.NotContain("password");
        AssertNoCollection();
    }

    [Fact]
    public async Task XML_base_is_refused_before_any_import_is_followed()
    {
        _routes.Items["/service.wsdl"] = new(_routes.Items["/service.wsdl"].Body.Replace(
            "<definitions ", "<definitions xml:base='https://internal.test/' ", StringComparison.Ordinal));
        Func<Task> export = async () => _ = await Export();

        await export.Should().ThrowAsync<WsdlExportException>();
        _routes.Requests.Should().ContainSingle();
        AssertNoCollection();
    }

    [Fact]
    public async Task Timeout_cancels_the_whole_collection()
    {
        _routes.Delay = true;
        Func<Task> export = async () => _ = await Export(options: new(Snapshot,
            Limits: WsdlLimits.Default with { FetchTimeoutSeconds = 1 }));

        await export.Should().ThrowAsync<WsdlExportException>().WithMessage("*COLLECTION_SERVICEUNAVAILABLE*");
        AssertNoCollection();
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved_and_cleans_up()
    {
        _routes.Delay = true;
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));
        Func<Task> export = async () => _ = await _exporter.ExportUrlAsync(new Uri("https://contracts.test/service.wsdl"),
            new(Snapshot), "SOAP acceptance", "1.0", cancellation.Token);

        await export.Should().ThrowAsync<OperationCanceledException>();
        AssertNoCollection();
    }

    [Theory]
    [InlineData("sources")]
    [InlineData("offline")]
    [InlineData("openapi.yaml")]
    public async Task A_modified_snapshot_is_not_silently_replayed(string part)
    {
        _ = await Export();
        string path = part.EndsWith(".yaml", StringComparison.Ordinal) ? Path.Combine(Snapshot, part)
            : Directory.GetFiles(Path.Combine(Snapshot, part))[0];
        await File.AppendAllTextAsync(path, "changed");
        Func<Task> replay = async () => _ = await _exporter.ExportAsync(Path.Combine(Snapshot, "manifest.json"),
            "SOAP acceptance", "1.0", CancellationToken.None);

        await replay.Should().ThrowAsync<WsdlExportException>().WithMessage("*SNAPSHOT_CHANGED*");
    }

    [Fact]
    public async Task Manifest_paths_cannot_escape_the_snapshot()
    {
        _ = await Export();
        string manifestPath = Path.Combine(Snapshot, "manifest.json");
        JsonNode manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
        manifest["Documents"]![0]!["Name"] = "../outside.xml";
        manifest["Entry"] = "../outside.xml";
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString());
        Func<Task> replay = async () => _ = await _exporter.ExportAsync(manifestPath, "SOAP acceptance", "1.0", CancellationToken.None);

        await replay.Should().ThrowAsync<WsdlExportException>().WithMessage("*SNAPSHOT_INVALID*");
    }

    [Fact]
    public async Task Existing_collection_is_preserved_before_any_network_request()
    {
        Directory.CreateDirectory(Snapshot);
        string sentinel = Path.Combine(Snapshot, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "original");
        Func<Task> export = async () => _ = await Export();

        await export.Should().ThrowAsync<WsdlExportException>();
        _routes.Requests.Should().BeEmpty();
        (await File.ReadAllTextAsync(sentinel)).Should().Be("original");
    }

    [Fact]
    public async Task A_symlink_cannot_replace_a_verified_replay_document()
    {
        _ = await Export();
        string document = Directory.GetFiles(Path.Combine(Snapshot, "offline"))[0];
        string outside = Path.Combine(_root, "outside.xml");
        File.Move(document, outside);
        File.CreateSymbolicLink(document, outside);
        Func<Task> replay = async () => _ = await _exporter.ExportAsync(Path.Combine(Snapshot, "manifest.json"),
            "SOAP acceptance", "1.0", CancellationToken.None);

        await replay.Should().ThrowAsync<WsdlExportException>().WithMessage("*SNAPSHOT_INVALID*");
    }

    [Fact]
    public async Task Declining_plain_HTTP_cancels_without_a_request_or_collection()
    {
        TestConsole console = new();
        console.Profile.Capabilities.Interactive = true;
        foreach (string input in new[] { "http://contracts.test/service.wsdl", Path.Combine(_root, "result.yaml"), "SOAP", "1.0", Snapshot, "", "", "n" })
        {
            console.Input.PushTextWithEnter(input);
        }

        ToolRun run = await new WsdlOpenApiTool(_exporter, console).RunAsync(CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Cancelled);
        _routes.Requests.Should().BeEmpty();
        AssertNoCollection();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Console_URL_export_publishes_validated_yaml_and_can_target_the_collection_artifact(bool inside)
    {
        string target = inside ? Path.Combine(Snapshot, "openapi.yaml") : Path.Combine(_root, "result.yaml");
        TestConsole console = new();
        console.Profile.Capabilities.Interactive = true;
        foreach (string input in new[] { "https://contracts.test/service.wsdl", target, "SOAP acceptance", "1.0", Snapshot, "", "" })
        {
            console.Input.PushTextWithEnter(input);
        }

        ToolRun run = await new WsdlOpenApiTool(_exporter, console).RunAsync(CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Success, console.Output);
        (await File.ReadAllTextAsync(target)).Should().Contain("openapi: 3.0.3");
        console.Output.Should().NotContain("private.example");
        Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    private Task<string> Export(string source = "https://contracts.test/service.wsdl", WsdlUrlExportOptions? options = null) =>
        _exporter.ExportUrlAsync(new Uri(source), options ?? new(Snapshot), "SOAP acceptance", "1.0", CancellationToken.None);

    private void AssertNoCollection()
    {
        Directory.Exists(Snapshot).Should().BeFalse();
        Directory.GetFileSystemEntries(_root).Should().BeEmpty();
    }

    public void Dispose()
    {
        _client.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }
    private sealed record Route(string Body, string? Location = null);

    private sealed class Routes : HttpMessageHandler
    {
        internal Dictionary<string, Route> Items { get; } = new(StringComparer.Ordinal);
        internal List<HttpRequestMessage> Requests { get; } = [];
        internal List<string> Pinned { get; } = [];
        internal Stream? Body { get; set; }
        internal long? DeclaredLength { get; set; }
        internal bool Delay { get; set; }

        internal void Graph(string prefix = "")
        {
            foreach (string path in new[] { "service.wsdl", "bindings/shared.wsdl", "model/contract.wsdl", "schema/a/types.xsd", "schema/a/extra.xsd", "schema/b/types.xsd" })
            {
                Items[prefix + "/" + path] = new(CorpusLoader.Read("Wsdl.url." + path.Replace('/', '.')));
            }
        }

        internal void EntryImport(string location) => Items["/service.wsdl"] = new(Items["/service.wsdl"].Body
            .Replace("bindings/shared.wsdl", location, StringComparison.Ordinal));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Pinned.AddRange(PinnedAddress.Of(request) ?? []);
            if (Delay)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            Route route = Items[request.RequestUri!.AbsolutePath];
            HttpResponseMessage response = new(route.Location is null ? HttpStatusCode.OK : HttpStatusCode.Found)
            {
                RequestMessage = request,
                Content = Body is null ? new StringContent(route.Body, Encoding.UTF8, "text/xml") : new StreamContent(Body),
            };
            if (route.Location is not null)
            {
                response.Headers.Location = new Uri(route.Location, UriKind.RelativeOrAbsolute);
            }

            if (DeclaredLength.HasValue)
            {
                response.Content.Headers.ContentLength = DeclaredLength;
            }

            return response;
        }
    }

    private sealed class CountingBody(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        internal int Received { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = base.Read(buffer.Span);
            Received += read;
            return ValueTask.FromResult(read);
        }
    }
}
