using System.Net;
using DevToolbox.Application.Abstractions;
using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Http;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Http;

/// <summary>
/// Le graphe se récupère-t-il, s'écrit-il, et se relit-il comme s'il avait été donné par chemin ?
/// </summary>
/// <remarks>
/// Contre un vrai serveur en boucle locale, et non contre un gestionnaire simulé. Ce qui est éprouvé ici
/// n'est pas que le code appelle la bonne méthode, mais que le tour complet fonctionne : une requête, une
/// réponse, un fichier écrit, un import résolu contre une adresse, puis un contrat lu.
/// <para>
/// Ces essais doivent confirmer <c>127.0.0.1</c> dans leur politique, parce que le garde refuse la boucle
/// locale sans confirmation. C'est une contrainte imposée par la règle à l'essai de la règle, et c'est
/// bon signe : si l'essai pouvait atteindre la boucle locale sans rien dire, le garde ne servirait à rien.
/// </para>
/// </remarks>
public sealed class ContractFetcherTests : IAsyncLifetime, IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-recup-" + Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, Served> _served = new(StringComparer.Ordinal);
    private readonly List<string> _requested = [];

    private HttpListener _listener = null!;
    private string _origin = null!;
    private Task _serving = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);

        int port = Free();

        _origin = "http://127.0.0.1:" + port + "/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_origin);
        _listener.Start();

        _serving = ServeAsync();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_whole_graph_is_written_then_read_back_from_the_files()
    {
        Serve("contrats/v2/catalogue.wsdl", "Wsdl.catalogue.wsdl");
        Serve("contrats/v2/catalogue-types.xsd", "Wsdl.catalogue-types.xsd");
        Serve("contrats/v2/commun.xsd", "Wsdl.commun.xsd");

        Result<WsdlContract> fetched = await FetchAsync("contrats/v2/catalogue.wsdl");

        fetched.IsSuccess.Should().BeTrue(fetched.Failure?.Message);

        WsdlContract contract = fetched.Value;

        contract.Documents.Should().HaveCount(3, "le wsdl et les deux schémas");
        contract.Operations.Should().HaveCount(5);
        contract.IsComplete.Should().BeTrue();

        Directory.GetFiles(_root).Should().HaveCount(
            3, "tout a été matérialisé, et rien de partiel n'est resté");
    }

    [Fact]
    public async Task A_materialised_document_keeps_the_bytes_the_server_served()
    {
        Serve("catalogue.wsdl", "Wsdl.catalogue.wsdl");
        Serve("catalogue-types.xsd", "Wsdl.catalogue-types.xsd");
        Serve("commun.xsd", "Wsdl.commun.xsd");

        Result<WsdlContract> fetched = await FetchAsync("catalogue.wsdl");

        fetched.IsSuccess.Should().BeTrue(fetched.Failure?.Message);

        // Octet pour octet : l'empreinte ne vaut que si le fichier est ce que le serveur a envoyé. C'est
        // la raison pour laquelle les « schemaLocation » ne sont pas réécrits.
        string written = Path.Combine(_root, "catalogue.wsdl");

        string onDisk = await File.ReadAllTextAsync(written);

        onDisk.Should().Be(CorpusLoader.Read("Wsdl.catalogue.wsdl"));
    }

    [Fact]
    public async Task Two_documents_with_the_same_file_name_do_not_overwrite_each_other()
    {
        // Ici le document homonyme devient enfin hostile : lus depuis le disque, les deux « types.xsd »
        // gardaient leur dossier ; matérialisés à plat, le second écraserait le premier en silence.
        Serve("same-name.wsdl", "Wsdl.hostile.same-name.wsdl");
        Serve("same-name/a/types.xsd", "Wsdl.hostile.same_name.a.types.xsd");
        Serve("same-name/b/types.xsd", "Wsdl.hostile.same_name.b.types.xsd");

        Result<WsdlContract> fetched = await FetchAsync("same-name.wsdl");

        fetched.IsSuccess.Should().BeTrue(fetched.Failure?.Message);

        fetched.Value.Types.Keys.Should()
            .Contain("http://entreprise.local/homonymes/a:Jetona")
            .And.Contain("http://entreprise.local/homonymes/b:Jetonb",
                "les deux ont survécu, donc aucun n'a été écrasé");

        Directory.GetFiles(_root).Should().HaveCount(3);
    }

    [Fact]
    public async Task A_relative_import_is_resolved_against_the_url_it_came_from()
    {
        // Le document est servi depuis un sous-dossier et importe « ../commun/partage.xsd ». Résolu
        // contre le dossier à plat, ce chemin ne désignerait rien ; résolu contre l'adresse, il désigne
        // ce que le serveur sert.
        ServeBody("contrats/v2/service.wsdl", Wrapping("../commun/partage.xsd"));
        ServeBody("contrats/commun/partage.xsd", Shared());

        Result<WsdlContract> fetched = await FetchAsync("contrats/v2/service.wsdl");

        fetched.IsSuccess.Should().BeTrue(fetched.Failure?.Message);
        fetched.Value.Types.Keys.Should().Contain("http://entreprise.local/partage:Jeton");

        _requested.Should().Contain(
            "/contrats/commun/partage.xsd", "l'adresse demandée est celle que l'import désigne");
    }

    [Fact]
    public async Task A_sign_in_page_returned_with_a_success_status_is_refused_by_naming_it()
    {
        _served["/catalogue.wsdl"] = new Served(
            "<!DOCTYPE html><html><body><form>Connexion</form></body></html>", "text/html");

        Result<WsdlContract> fetched = await FetchAsync("catalogue.wsdl");

        fetched.IsSuccess.Should().BeFalse();
        fetched.Failure!.Message.Should().Contain("text/html")
            .And.Contain("ne forment pas un document XML")
            .And.Contain("page de connexion", "c'est l'échec ordinaire, et il est nommé comme tel");
    }

    [Fact]
    public async Task An_address_asking_for_credentials_is_refused_with_what_to_do_instead()
    {
        _served["/catalogue.wsdl"] = new Served("", "text/plain", HttpStatusCode.Unauthorized);

        Result<WsdlContract> fetched = await FetchAsync("catalogue.wsdl");

        fetched.IsSuccess.Should().BeFalse();
        fetched.Failure!.Reason.Should().Be(FailureReason.AuthenticationFailed);
        fetched.Failure.Message.Should().Contain("passez-le comme fichier");
    }

    [Fact]
    public async Task A_response_over_the_budget_is_refused_with_the_limit_named()
    {
        _served["/gros.wsdl"] = new Served(new string('x', 4096), "application/xml");

        Result<WsdlContract> fetched = await FetchAsync(
            "gros.wsdl", WsdlLimits.Default with { MaxResponseBytes = 512 });

        fetched.IsSuccess.Should().BeFalse();
        fetched.Failure!.Message.Should().Contain("512");

        Directory.GetFiles(_root).Should().BeEmpty("rien de partiel n'a été laissé derrière");
    }

    [Fact]
    public async Task Plain_http_to_the_loopback_still_needs_both_the_scheme_and_the_address()
    {
        // L'essai de la règle par la règle. Sans la confirmation, le garde refuse — et c'est la preuve
        // que les essais précédents ne passaient pas par une porte ouverte.
        Serve("catalogue.wsdl", "Wsdl.catalogue.wsdl");

        Result<WsdlContract> withoutAddress = await FetchAsync(
            "catalogue.wsdl", WsdlLimits.Default, Policy() with { ConfirmedAddresses = [] });

        withoutAddress.IsSuccess.Should().BeFalse();
        withoutAddress.Failure!.Message.Should().Contain("127.0.0.1");

        Result<WsdlContract> withoutScheme = await FetchAsync(
            "catalogue.wsdl", WsdlLimits.Default, Policy() with { AllowPlainHttp = false });

        withoutScheme.IsSuccess.Should().BeFalse();
        withoutScheme.Failure!.Message.Should().Contain("en clair");
    }

    /// <summary>Libère le serveur, que l'analyseur exige de voir libéré explicitement.</summary>
    public void Dispose() => ((IDisposable)_listener).Dispose();

    public async Task DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();

        try
        {
            await _serving.ConfigureAwait(false);
        }
        catch (HttpListenerException)
        {
            // Le serveur s'arrête en levant sur l'attente en cours. C'est sa façon de s'arrêter.
        }
        catch (ObjectDisposedException)
        {
            // Idem.
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static int Free()
    {
        using System.Net.Sockets.Socket probe = new(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp);

        probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        return ((IPEndPoint)probe.LocalEndPoint!).Port;
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);

            _requested.Add(context.Request.Url!.AbsolutePath);

            Served? found = _served.GetValueOrDefault(context.Request.Url.AbsolutePath);

            byte[] body = Encoding.UTF8.GetBytes(found?.Body ?? "introuvable");

            context.Response.StatusCode = (int)(found?.Status ?? HttpStatusCode.NotFound);
            context.Response.ContentType = found?.ContentType ?? "text/plain";
            context.Response.ContentLength64 = body.Length;

            await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);

            context.Response.Close();
        }
    }

    /// <summary>Sert un document du corpus à cette adresse.</summary>
    private void Serve(string path, string resource) =>
        ServeBody(path, CorpusLoader.Read(resource));

    /// <summary>Sert un document écrit ici même à cette adresse.</summary>
    private void ServeBody(string path, string body) =>
        _served["/" + path] = new Served(body, "application/xml");

    private static string Wrapping(string imported) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
            xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:service">
          <wsdl:types>
            <xs:schema targetNamespace="urn:service">
              <xs:import namespace="http://entreprise.local/partage" schemaLocation="{imported}" />
            </xs:schema>
          </wsdl:types>
        </wsdl:definitions>
        """;

    private static string Shared() => """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
            targetNamespace="http://entreprise.local/partage">
          <xs:simpleType name="Jeton">
            <xs:restriction base="xs:string">
              <xs:maxLength value="8"/>
            </xs:restriction>
          </xs:simpleType>
        </xs:schema>
        """;

    private ImportPolicy Policy() => new(
        _root,
        AllowRemote: true,
        AllowedHosts: ["127.0.0.1"],
        AllowPlainHttp: true,
        ConfirmedAddresses: ["127.0.0.1"]);

    private Task<Result<WsdlContract>> FetchAsync(string path) =>
        FetchAsync(path, WsdlLimits.Default);

    private Task<Result<WsdlContract>> FetchAsync(string path, WsdlLimits limits) =>
        FetchAsync(path, limits, Policy());

    private Task<Result<WsdlContract>> FetchAsync(
        string path, WsdlLimits limits, ImportPolicy policy)
    {
        ContractFetcher fetcher = new(
            new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }),
            new AddressGuard(),
            new WsdlReader(),
            new FrozenClock());

        return fetcher.FetchAsync(
            new Uri(_origin + path), _root, limits, policy, CancellationToken.None);
    }

    private sealed record Served(
        string Body, string ContentType, HttpStatusCode Status = HttpStatusCode.OK);

    private sealed class FrozenClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
    }
}
