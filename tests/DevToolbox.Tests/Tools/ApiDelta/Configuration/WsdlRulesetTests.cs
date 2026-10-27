using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Configuration;

/// <summary>
/// Ce qu'une équipe peut régler de la lecture d'un contrat de service, et ce qui lui est refusé.
/// </summary>
/// <remarks>
/// Les trois épreuves qui comptent ici ne portent pas sur les valeurs mais sur les silences. Une clé
/// qu'on croit avoir posée et que l'outil n'a jamais lue est pire qu'une clé absente : le réglage n'agit
/// pas, et on cherche la raison du résultat partout sauf dans le fichier. Une configuration absente doit
/// rester utilisable, sans quoi l'outil ne sert qu'à qui l'a déjà configuré. Et le réseau permis sans
/// liste d'hôtes doit être refusé plutôt que lu comme « tous ».
/// </remarks>
public sealed class WsdlRulesetTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-jeu-wsdl-" + Guid.NewGuid().ToString("N"));

    public WsdlRulesetTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task No_configuration_at_all_still_gives_usable_settings()
    {
        WsdlSettings settings = Ruleset.Default.Wsdl;

        settings.Limits.Should().Be(WsdlLimits.Default);
        settings.Imports.AllowRemote.Should().BeFalse("le distant est fermé tant qu'on ne l'ouvre pas");
        settings.Table.Should().BeNull("aucune table écrite veut dire celle qui est livrée");

        settings.ConfinedTo("/un/dossier").ConfinementRoot.Should().Be(
            "/un/dossier", "la racine est une donnée de l'exécution, pas une décision d'équipe");

        // Et le fichier vide ne doit pas non plus dégrader quoi que ce soit.
        Ruleset read = await ReadAsync("wsdl:\n  limits:\n    maxDocumentCount: 12\n");

        read.Wsdl.Limits.MaxDocumentCount.Should().Be(12);
        read.Wsdl.Limits.MaxNestingDepth.Should().Be(
            WsdlLimits.Default.MaxNestingDepth, "ce qui n'est pas écrit garde sa valeur livrée");
    }

    [Fact]
    public async Task Every_budget_and_every_import_choice_can_be_set()
    {
        Ruleset read = await ReadAsync(
            """
            wsdl:
              limits:
                maxDocumentBytes: 2048
                maxNestingDepth: 12
                maxImportChainDepth: 3
                maxDocumentCount: 7
                maxEnumerationValues: 99
                maxResponseBytes: 4096
                fetchTimeoutSeconds: 10
              imports:
                root: /contrats
                allowRemote: true
                allowedHosts: [services.entreprise.local]
                allowPlainHttp: true
                maxRedirects: 2
                confirmedAddresses: [10.0.0.7]
            """);

        read.Wsdl.Limits.Should().Be(new WsdlLimits(2048, 12, 3, 7, 99, 4096, 10));

        read.Wsdl.Imports.AllowRemote.Should().BeTrue();
        read.Wsdl.Imports.AllowedHosts.Should().Equal("services.entreprise.local");
        read.Wsdl.Imports.AllowPlainHttp.Should().BeTrue();
        read.Wsdl.Imports.MaxRedirects.Should().Be(2);
        read.Wsdl.Imports.ConfirmedAddresses.Should().Equal("10.0.0.7");

        read.Wsdl.ConfinedTo("/ailleurs").ConfinementRoot.Should().Be(
            "/contrats", "la racine écrite par l'équipe l'emporte sur le dossier du contrat");
    }

    [Fact]
    public async Task A_written_verb_table_replaces_the_shipped_one()
    {
        // Remplace, et ne complète pas. Une équipe qui écrit trois verbes veut ses trois verbes : les
        // ajouter aux trente-six livrés lui ferait produire des itinéraires qu'elle n'a pas demandés.
        Ruleset read = await ReadAsync(
            """
            wsdl:
              verbs:
                consulter: GET
                televerser: post
            """);

        read.Wsdl.Verbs.Should().HaveCount(2);

        RouteDerivation.Derive("televerserDocument", read.Wsdl.Table).Should().Be(
            new DerivedRoute("POST", "/documents", RouteDerivationKind.Derived, "televerser"));

        RouteDerivation.Derive("deposerDocument", read.Wsdl.Table).Kind.Should().Be(
            RouteDerivationKind.Partial, "« deposer » ne figure plus dans la table de l'équipe");
    }

    [Fact]
    public async Task An_unknown_key_names_its_file_its_line_and_its_column_and_the_run_does_not_start()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            wsdl:
              limits:
                maxDocumentCount: 7
                maxDocumentsCount: 9
            """);

        Diagnostic named = refused.Should().ContainSingle().Subject;

        named.Message.Should().Contain("maxDocumentsCount");
        named.Message.Should().Contain(RulesetStore.ConventionalName, "le refus nomme son fichier");
        named.Position.Should().NotBeNull();
        named.Position.Line.Should().Be(4);
        named.Position.Column.Should().BeGreaterThan(1, "la colonne désigne la clé, pas la ligne");
    }

    [Fact]
    public async Task An_unknown_section_is_named_instead_of_being_read_as_another_one()
    {
        // Le repli d'avant lisait toute section inconnue comme si c'était « extract » : une faute de
        // frappe sur un nom de section n'avait aucun effet visible, et une équipe pouvait croire un
        // réglage en vigueur pendant des mois.
        IReadOnlyList<Diagnostic> refused = await RefusedAsync("wsdle:\n  limits:\n    maxDepth: 3\n");

        refused.Should().ContainSingle()
            .Which.Message.Should().Contain("wsdle").And.Contain("n'est pas reconnue");
    }

    [Fact]
    public async Task Remote_resolution_with_an_empty_host_list_is_refused()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            wsdl:
              imports:
                allowRemote: true
            """);

        refused.Should().ContainSingle()
            .Which.Message.Should().Contain("allowedHosts");
    }

    [Fact]
    public async Task A_verb_bound_to_something_that_is_not_a_method_is_refused()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            wsdl:
              verbs:
                consulter: LIRE
            """);

        refused.Should().ContainSingle()
            .Which.Message.Should().Contain("consulter").And.Contain("GET");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<Ruleset> ReadAsync(string content)
    {
        RulesetReadResult read = await ReadResultAsync(content);

        read.IsApplicable.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return read.Ruleset!;
    }

    /// <summary>Lit un jeu de règles qui doit être refusé, et rend ce qui l'explique.</summary>
    private async Task<IReadOnlyList<Diagnostic>> RefusedAsync(string content)
    {
        RulesetReadResult read = await ReadResultAsync(content);

        read.IsApplicable.Should().BeFalse(
            "ce fichier ne doit pas être applicable, et l'exécution ne doit pas démarrer");

        return read.Diagnostics;
    }

    private async Task<RulesetReadResult> ReadResultAsync(string content)
    {
        string path = Path.Combine(
            _root, Guid.NewGuid().ToString("N"), RulesetStore.ConventionalName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await File.WriteAllTextAsync(path, content).ConfigureAwait(true);

        return await new RulesetStore()
            .ReadAsync(path, Ruleset.Default, CancellationToken.None).ConfigureAwait(true);
    }
}
