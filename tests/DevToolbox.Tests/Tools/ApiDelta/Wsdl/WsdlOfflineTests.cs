using System.Net;
using System.Net.Http;
using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure;
using DevToolbox.Tools.ApiDelta.Infrastructure.Http;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Une exécution dont les entrées sont des fichiers appelle-t-elle quoi que ce soit ?
/// </summary>
/// <remarks>
/// Établi par observation, et non en relisant la configuration. Lire le code et conclure « il n'y a pas
/// d'appel » est exactement le raisonnement qui a laissé passer tous les appels sortants de l'histoire de
/// l'informatique : une bibliothèque qui résout un espace de noms, un analyseur qui va chercher une
/// définition de type de document, un client qui tente une découverte. L'épreuve pose donc un guetteur
/// <em>sous</em> le seul client de l'outil, et regarde s'il bouge.
/// <para>
/// Le guetteur est lui-même éprouvé : la seconde épreuve lui fait voir un appel. Un guetteur qu'on n'a
/// jamais vu réagir ne prouve rien — il pourrait n'être branché sur rien.
/// </para>
/// </remarks>
public sealed class WsdlOfflineTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-hors-ligne-" + Guid.NewGuid().ToString("N"));

    public WsdlOfflineTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Reading_projecting_and_pairing_from_files_makes_no_outbound_call()
    {
        Watcher watcher = new();

        using ServiceProvider provider = Provider(watcher);

        // Le graphe réel : le récupérateur existe, son client est construit, et rien ne l'appelle.
        provider.GetRequiredService<IContractFetcher>().Should().NotBeNull();

        WsdlContract contract = await ReadAsync().ConfigureAwait(true);

        ProjectionResult projected = new Projector().Project(contract, OpenApiVersion.V30);

        projected.Figures.Operations.Should().Be(5, "la chaîne a bien tourné en entier");

        watcher.Calls.Should().BeEmpty(
            "une exécution dont les entrées sont des fichiers n'appelle rien : "
            + string.Join(", ", watcher.Calls));
    }

    [Fact]
    public async Task The_watcher_would_have_seen_a_call_if_there_had_been_one()
    {
        Watcher watcher = new();

        using ServiceProvider provider = Provider(watcher);

        IContractFetcher fetcher = provider.GetRequiredService<IContractFetcher>();

        // La boucle locale, et confirmée : le garde d'adresses refuse un service interne que personne
        // n'a nommé, et il le refuse *avant* l'appel. Une adresse qu'il écarterait ne prouverait donc
        // rien du guetteur — elle prouverait seulement que le garde fait son travail, ce qui est
        // éprouvé ailleurs.
        _ = await fetcher.FetchAsync(
            new Uri("http://127.0.0.1:9/catalogue?wsdl"),
            Path.Combine(_root, "recupere"),
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root) with
            {
                AllowRemote = true,
                AllowedHosts = ["127.0.0.1"],
                AllowPlainHttp = true,
                ConfirmedAddresses = ["127.0.0.1"],
            },
            CancellationToken.None).ConfigureAwait(true);

        watcher.Calls.Should().NotBeEmpty("le guetteur doit voir passer un appel réel");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Le graphe du récupérateur, son gestionnaire primaire remplacé par le guetteur.</summary>
    private static ServiceProvider Provider(Watcher watcher)
    {
        ServiceCollection services = [];

        services.AddApiDeltaInfrastructure();
        _ = services.AddSingleton<DevToolbox.Application.Abstractions.IClock>(
            new DevToolbox.Infrastructure.Platform.SystemClock());

        _ = services.AddHttpClient<IContractFetcher, ContractFetcher>()
            .ConfigurePrimaryHttpMessageHandler(() => watcher);

        return services.BuildServiceProvider();
    }

    private async Task<WsdlContract> ReadAsync()
    {
        foreach (string name in new[] { "catalogue.wsdl", "catalogue-types.xsd", "commun.xsd" })
        {
            await File.WriteAllTextAsync(
                Path.Combine(_root, name),
                CorpusLoader.Read("Wsdl." + name),
                new UTF8Encoding(false)).ConfigureAwait(true);
        }

        WsdlReadResult read = await new WsdlReader().ReadAsync(
            Path.Combine(_root, "catalogue.wsdl"),
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).ConfigureAwait(true);

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(one => one.Message)));

        return read.Contract!;
    }

    /// <summary>Le guetteur : il note ce qu'on lui demande d'atteindre, et n'atteint rien.</summary>
    private sealed class Watcher : HttpMessageHandler
    {
        private readonly List<string> _calls = [];

        /// <summary>Les adresses qu'on a tenté d'atteindre.</summary>
        internal IReadOnlyList<string> Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            lock (_calls)
            {
                _calls.Add(request.RequestUri?.ToString() ?? "(sans adresse)");
            }

            // Un statut que le gestionnaire de résilience ne réessaie pas : avec « 503 », l'épreuve
            // attendait onze secondes de tentatives contre un guetteur qui ne répondra jamais mieux.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                RequestMessage = request,
                Content = new StringContent(string.Empty),
            });
        }
    }
}
