using System.Net;
using System.Net.Sockets;
using System.Text;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Xunit;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;

namespace DevToolbox.Tests.Tools.ApiDelta.Http;

/// <summary>
/// La connexion s'ouvre-t-elle sur l'adresse que le garde a validée, et sur rien d'autre ?
/// </summary>
/// <remarks>
/// Le garde résolvait un nom, validait les adresses obtenues, puis le client résolvait le nom une seconde
/// fois pour se connecter : rien ne garantissait que la seconde réponse fût la première. Un hôte autorisé
/// dont la réponse DNS bascule entre les deux atteignait une adresse que le garde n'avait jamais vue.
/// <para>
/// L'épinglage se fait dans le gestionnaire que la racine de composition configure, et aucun essai ne le
/// traversait : celui du récupérateur monte son propre client. Cet essai-ci passe par le graphe réel,
/// contre un vrai serveur en boucle locale, et c'est lui qui s'apercevrait qu'un épinglage cassé empêche
/// toute récupération.
/// </para>
/// </remarks>
public sealed class PinnedConnectionTests : IAsyncLifetime, IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-epingle-" + Guid.NewGuid().ToString("N"));

    private HttpListener _listener = null!;
    private string _origin = null!;
    private Task _serving = null!;
    private TimeSpan _delay = TimeSpan.Zero;
    private bool _exportGraph;
    private readonly List<string> _requested = [];

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);

        int port = Free();

        _origin = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _listener = new HttpListener();
        _listener.Prefixes.Add(_origin + "/");
        _listener.Start();

        _serving = ServeAsync();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_real_graph_retrieves_through_the_validated_address()
    {
        using ServiceProvider provider = Provider();

        IContractFetcher fetcher = provider.GetRequiredService<IContractFetcher>();

        Result<WsdlContract> fetched = await fetcher.FetchAsync(
            new Uri(_origin + "/catalogue?wsdl"),
            Path.Combine(_root, "recupere"),
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root) with
            {
                AllowRemote = true,
                AllowedHosts = ["127.0.0.1"],
                AllowPlainHttp = true,

                // Le garde refuse la boucle locale sans confirmation : c'est la règle, et cet essai la
                // subit comme un développeur la subirait.
                ConfirmedAddresses = ["127.0.0.1"],
            },
            CancellationToken.None).ConfigureAwait(true);

        fetched.IsSuccess.Should().BeTrue(
            "la connexion doit s'ouvrir sur l'adresse validée — " + fetched.Failure?.Message);

        fetched.Value.Operations.Should().ContainSingle()
            .Which.Name.Should().Be("lireChose");
    }

    [Fact]
    public async Task The_configured_retrieval_budget_bites_and_names_itself()
    {
        // Le budget vivait dans le gestionnaire de résilience, figé à l'enregistrement : un
        // « wsdl.limits.fetchTimeoutSeconds » écrit par une équipe n'avait aucun effet, et un graphe de
        // vingt imports consommait vingt fois le délai d'une seule requête. Un budget qui ne mord pas
        // donne la preuve d'une protection qu'on n'a pas.
        _delay = TimeSpan.FromSeconds(8);

        using ServiceProvider provider = Provider();

        Result<WsdlContract> fetched = await provider.GetRequiredService<IContractFetcher>().FetchAsync(
            new Uri(_origin + "/lent?wsdl"),
            Path.Combine(_root, "recupere"),
            WsdlLimits.Default with { FetchTimeoutSeconds = 1 },
            ImportPolicy.ConfinedTo(_root) with
            {
                AllowRemote = true,
                AllowedHosts = ["127.0.0.1"],
                AllowPlainHttp = true,
                ConfirmedAddresses = ["127.0.0.1"],
            },
            CancellationToken.None).ConfigureAwait(true);

        fetched.IsSuccess.Should().BeFalse("une seconde ne suffit pas à un serveur qui en prend trente");
        fetched.Failure!.Message.Should().Contain("fetchTimeoutSeconds");
    }

    [Fact]
    public async Task The_registered_exporter_collects_a_real_multi_document_URL_graph_and_replays_it()
    {
        _exportGraph = true;
        using ServiceProvider provider = Provider();
        IWsdlOpenApiExporter exporter = provider.GetRequiredService<IWsdlOpenApiExporter>();
        string collection = Path.Combine(_root, "capture");

        string yaml = await exporter.ExportUrlAsync(new Uri(_origin + "/service.wsdl"),
            new(collection, ConfirmedAddresses: ["127.0.0.1"], AllowPlainHttp: true),
            "SOAP acceptance", "1.0", CancellationToken.None);

        yaml.Should().Be(CorpusLoader.Read("Wsdl.url.expected.openapi.yaml"));
        _requested.Should().HaveCount(6);
        string replay = await exporter.ExportAsync(Path.Combine(collection, "manifest.json"),
            "SOAP acceptance", "1.0", CancellationToken.None);
        replay.Should().Be(yaml);
        _requested.Should().HaveCount(6, "le rejeu n'ouvre aucune connexion");
    }

    public async Task DisposeAsync()
    {
        _listener.Stop();

        await _serving.ConfigureAwait(false);

        _listener.Close();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_proxy_on_the_same_host_cannot_change_the_pinned_destination_port()
    {
        bool configured = false;
        TcpListener proxy = new(IPAddress.Loopback, 0);
        proxy.Start();
        int proxyPort = ((IPEndPoint)proxy.LocalEndpoint).Port;
        ServiceCollection services = [];
        services.AddApiDeltaInfrastructure();
        services.AddSingleton<IClock>(new DevToolbox.Infrastructure.Platform.SystemClock());
        services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            SocketsHttpHandler transport = (SocketsHttpHandler)builder.PrimaryHandler;
            transport.Proxy = new WebProxy(new Uri("http://127.0.0.1:" + proxyPort.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                { BypassProxyOnLocal = false };
            transport.UseProxy = true;
            configured = true;
        }));
        using ServiceProvider provider = services.BuildServiceProvider();
        Uri source = new("http://127.0.0.1:" + Free().ToString(System.Globalization.CultureInfo.InvariantCulture) + "/service.wsdl");
        Func<Task> export = async () => _ = await provider.GetRequiredService<IWsdlOpenApiExporter>().ExportUrlAsync(source,
            new(Path.Combine(_root, "capture"), ConfirmedAddresses: ["127.0.0.1"], AllowPlainHttp: true,
                Limits: WsdlLimits.Default with { FetchTimeoutSeconds = 1 }), "SOAP acceptance", "1.0", CancellationToken.None);

        try
        {
            await export.Should().ThrowAsync<WsdlExportException>();
            configured.Should().BeTrue();
            proxy.Pending().Should().BeFalse("le transport refuse le proxy avant d'ouvrir une connexion vers un autre port");
            Directory.GetFileSystemEntries(_root).Should().BeEmpty();
        }
        finally
        {
            proxy.Stop();
        }
    }

    private static ServiceProvider Provider()
    {
        ServiceCollection services = [];

        services.AddApiDeltaInfrastructure();

        _ = services.AddSingleton<IClock>(new DevToolbox.Infrastructure.Platform.SystemClock());

        return services.BuildServiceProvider();
    }

    private static int Free()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);

        probe.Start();

        int port = ((IPEndPoint)probe.LocalEndpoint).Port;

        probe.Stop();

        return port;
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            // Le serveur lent sert à faire mordre le budget, et l'essai se termine avant lui : la
            // réponse part donc dans le vide, et son écriture doit se taire plutôt que faire échouer un
            // essai qui a déjà conclu.
            try
            {
                if (_delay > TimeSpan.Zero)
                {
                    await Task.Delay(_delay).ConfigureAwait(false);
                }

                string path = context.Request.Url!.AbsolutePath.TrimStart('/');
                _requested.Add(path);
                byte[] body = Encoding.UTF8.GetBytes(_exportGraph
                    ? CorpusLoader.Read("Wsdl.url." + path.Replace('/', '.')) : Contract);

                context.Response.ContentType = "text/xml";
                context.Response.ContentLength64 = body.Length;

                await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);

                context.Response.Close();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }
        }
    }

    /// <summary>Le plus petit contrat qui se lise : une opération, aucun import.</summary>
    private const string Contract = """
        <?xml version="1.0" encoding="utf-8"?>
        <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
            xmlns:xs="http://www.w3.org/2001/XMLSchema"
            xmlns:tns="urn:chose" targetNamespace="urn:chose">
          <wsdl:types>
            <xs:schema targetNamespace="urn:chose">
              <xs:element name="choseRequete" type="xs:string"/>
              <xs:element name="choseReponse" type="xs:string"/>
            </xs:schema>
          </wsdl:types>
          <wsdl:message name="lireChoseIn">
            <wsdl:part name="corps" element="tns:choseRequete"/>
          </wsdl:message>
          <wsdl:message name="lireChoseOut">
            <wsdl:part name="corps" element="tns:choseReponse"/>
          </wsdl:message>
          <wsdl:portType name="ChosePort">
            <wsdl:operation name="lireChose">
              <wsdl:input message="tns:lireChoseIn"/>
              <wsdl:output message="tns:lireChoseOut"/>
            </wsdl:operation>
          </wsdl:portType>
        </wsdl:definitions>
        """;
}
