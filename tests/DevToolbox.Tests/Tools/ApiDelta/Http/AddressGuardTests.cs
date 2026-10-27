using System.Net;
using System.Net.Sockets;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Http;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Http;

/// <summary>
/// Le jeu d'adresses hostiles : chacune refusée ou admise comme son exigence le dit.
/// </summary>
/// <remarks>
/// Ces adresses sont la version réseau du corpus hostile. Elles ne demandent pas que l'outil résiste,
/// mais qu'il <em>dise</em> ce qu'il refuse et pourquoi : un refus muet envoie chercher la panne dans le
/// contrat, et un refus qui parle de syntaxe quand il s'agit de périmètre est pire que pas de refus.
/// </remarks>
public sealed class AddressGuardTests
{
    private const string Root = "http://entreprise.local";

    [Theory]
    [InlineData("file:///c:/secrets/contrat.wsdl")]
    [InlineData("ftp://entreprise.local/contrat.wsdl")]
    [InlineData("gopher://entreprise.local/contrat.wsdl")]
    public async Task A_scheme_the_tool_does_not_reach_is_refused_by_naming_it(string address)
    {
        AddressCheck check = await CheckAsync(address);

        check.IsAllowed.Should().BeFalse();
        check.Refusal!.Code.Should().Be(WsdlDiagnosticCodes.SchemeRefused);
        check.Refusal.Message.Should().Contain(
            new Uri(address).Scheme, "le modèle est nommé, pas l'adresse déclarée invalide");
    }

    [Fact]
    public async Task Plain_http_is_refused_until_the_developer_asks_for_it()
    {
        AddressCheck refused = await CheckAsync("http://exemple.test/contrat.wsdl");

        refused.IsAllowed.Should().BeFalse();
        refused.Refusal!.Code.Should().Be(WsdlDiagnosticCodes.SchemeRefused);
        refused.Refusal.Message.Should().Contain("en clair");

        AddressCheck asked = await CheckAsync(
            "http://exemple.test/contrat.wsdl", Policy() with { AllowPlainHttp = true });

        asked.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Https_to_a_public_address_needs_nothing_more()
    {
        AddressCheck check = await CheckAsync("https://exemple.test/contrat.wsdl");

        check.IsAllowed.Should().BeTrue();
        check.Resolved.Should().ContainSingle()
            .Which.Should().Be("203.0.113.7", "les adresses sont dites même quand ça passe");
    }

    [Theory]
    [InlineData("127.0.0.1", "la boucle locale")]
    [InlineData("10.1.2.3", "la première plage privée")]
    [InlineData("172.20.0.9", "la deuxième")]
    [InlineData("192.168.1.5", "la troisième")]
    [InlineData("169.254.3.4", "le lien local")]
    [InlineData("100.70.0.1", "l'espace partagé des fournisseurs")]
    [InlineData("::1", "la boucle locale d'IPv6")]
    [InlineData("fd00::1", "une adresse locale unique d'IPv6")]
    public async Task An_internal_address_is_refused_until_it_is_confirmed_by_name(
        string resolved, string why)
    {
        AddressCheck refused = await CheckAsync("https://interne.test/contrat.wsdl", Policy(), resolved);

        refused.IsAllowed.Should().BeFalse(why + " est interne");
        refused.Refusal!.Code.Should().Be(WsdlDiagnosticCodes.AddressInternal);
        refused.Refusal.Message.Should().Contain(resolved, "l'adresse refusée est nommée");

        AddressCheck confirmed = await CheckAsync(
            "https://interne.test/contrat.wsdl",
            Policy() with { ConfirmedAddresses = [resolved] },
            resolved);

        confirmed.IsAllowed.Should().BeTrue("un service interne est le cas courant, pas l'exception");
    }

    [Fact]
    public async Task A_public_name_resolving_to_a_private_address_is_still_refused()
    {
        // L'essai qui justifie à lui seul d'injecter la résolution : aucun vrai résolveur ne produira
        // cette réponse, et c'est exactement le mécanisme par lequel on fait sortir un outil de son
        // périmètre. Comparer des chaînes de caractères n'y changerait rien.
        AddressCheck check = await CheckAsync(
            "https://parfaitement-public.test/contrat.wsdl", Policy(), "192.168.0.42");

        check.IsAllowed.Should().BeFalse();
        check.Refusal!.Code.Should().Be(WsdlDiagnosticCodes.AddressInternal);
        check.Refusal.Message.Should().Contain("192.168.0.42");
    }

    [Fact]
    public async Task Confirming_one_address_does_not_confirm_the_others_the_name_resolves_to()
    {
        // Un nom peut rendre plusieurs adresses. Confirmer la première n'autorise pas la seconde, sans
        // quoi la confirmation nommerait une adresse pour en ouvrir une autre.
        AddressCheck check = await CheckAsync(
            "https://deux.test/contrat.wsdl",
            Policy() with { ConfirmedAddresses = ["10.0.0.1"] },
            "10.0.0.1",
            "192.168.5.5");

        check.IsAllowed.Should().BeFalse();
        check.Refusal!.Message.Should().Contain("192.168.5.5");
    }

    [Fact]
    public async Task A_name_that_does_not_resolve_is_refused_by_saying_so()
    {
        AddressGuard guard = new((_, _) => throw new SocketException(11001));

        AddressCheck check = await guard.CheckAsync(
            new Uri("https://nulle-part.test/contrat.wsdl"), Policy(), CancellationToken.None);

        check.IsAllowed.Should().BeFalse();
        check.Refusal!.Code.Should().Be(WsdlDiagnosticCodes.AddressUnresolved);
        check.Refusal.Message.Should().Contain("nulle-part.test");
    }

    [Fact]
    public async Task An_address_literal_is_checked_without_asking_any_resolver()
    {
        // Une adresse écrite en clair n'a pas de nom à résoudre, et un garde qui appellerait quand même
        // le résolveur ferait sortir une requête pour rien — observable, et donc évitable.
        int asked = 0;

        AddressGuard guard = new((_, _) =>
        {
            asked++;

            return Task.FromResult<IPAddress[]>([IPAddress.Parse("203.0.113.1")]);
        });

        AddressCheck check = await guard.CheckAsync(
            new Uri("https://10.0.0.5/contrat.wsdl"), Policy(), CancellationToken.None);

        asked.Should().Be(0, "il n'y avait rien à résoudre");
        check.IsAllowed.Should().BeFalse("et l'adresse écrite est bien interne");
    }

    [Fact]
    public async Task No_refusal_ever_carries_the_query_string_of_the_address()
    {
        // Un jeton de service vit dans la chaîne de requête bien plus souvent qu'on ne le voudrait. Un
        // refus qui recopierait l'adresse entière le porterait jusqu'au journal.
        AddressCheck check = await CheckAsync(
            "ftp://entreprise.local/contrat.wsdl?jeton=tres-secret&cle=aussi");

        check.Refusal!.Message.Should().NotContain("tres-secret").And.NotContain("aussi");
    }

    private static ImportPolicy Policy() =>
        new(Root, AllowRemote: true, AllowedHosts: ["exemple.test"]);

    private static Task<AddressCheck> CheckAsync(string address) => CheckAsync(address, Policy());

    private static Task<AddressCheck> CheckAsync(
        string address, ImportPolicy policy, params string[] resolved)
    {
        IPAddress[] answers = resolved.Length == 0
            ? [IPAddress.Parse("203.0.113.7")]
            : [.. resolved.Select(IPAddress.Parse)];

        AddressGuard guard = new((_, _) => Task.FromResult(answers));

        return guard.CheckAsync(new Uri(address), policy, CancellationToken.None);
    }
}
