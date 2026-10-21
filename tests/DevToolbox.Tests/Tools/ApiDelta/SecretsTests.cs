using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tests.Tools.ApiDelta.Golden;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta;

/// <summary>
/// Ce qu'aucun artefact, aucun journal et aucun point de reprise ne doit porter.
/// </summary>
/// <remarks>
/// La promesse ne porte pas sur une reproduction fidèle que le développeur a demandée — un extrait ou une
/// copie optimisée sont le document, et si le document porte un mot de passe, la copie le porte aussi.
/// Elle porte sur ce que l'outil écrit <em>à propos</em> du document : les rapports, le plan, les
/// journaux, les points de reprise. C'est là que le caviardage se joue, et c'est là que la fuite serait
/// une faute de l'outil et non de l'entrée.
/// </remarks>
public sealed class SecretsTests : IDisposable
{
    /// <summary>Le mot de passe que <c>secrets.yaml</c> glisse dans l'URL d'un serveur.</summary>
    private const string Password = "Motdepasse-TRES-secret-42";

    /// <summary>Le jeton que <c>secrets.yaml</c> donne en exemple.</summary>
    private const string Token = "eyJhbGciOiJIUzI1NiwiJETON-DE-TEST-NE-PAS-DIFFUSER";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-secrets-" + Guid.NewGuid().ToString("N"));

    public SecretsTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task No_artifact_of_a_full_run_carries_the_literal_secret()
    {
        AuditRunHarness harness = new(_root, fileName: "secrets.yaml");

        await harness.RunAsync();

        IReadOnlyList<string> written =
        [
            .. Directory.EnumerateFiles(harness.Output, "*", SearchOption.AllDirectories),
        ];

        written.Should().NotBeEmpty("l'exécution doit avoir écrit quelque chose à inspecter");

        foreach (string path in written)
        {
            string content = await File.ReadAllTextAsync(path);

            content.Should().NotContain(
                Password, "« {0} » ne doit pas porter le mot de passe", Path.GetFileName(path));

            content.Should().NotContain(
                Token, "« {0} » ne doit pas porter le jeton", Path.GetFileName(path));
        }
    }

    [Fact]
    public async Task The_finding_names_the_location_and_the_pattern_but_never_the_value()
    {
        // Dire « un jeton porteur se trouve ici » suffit à aller le chercher. Le recopier ferait du
        // rapport une deuxième fuite.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "secrets.yaml");

        IReadOnlyList<Finding> found = AuditHarness.Of(audit, "AUD-306");

        found.Should().NotBeEmpty("les deux valeurs sensibles doivent être signalées");

        found.Select(finding => finding.Location.Json)
            .Should().Contain(pointer => pointer.Contains("/servers/", StringComparison.Ordinal))
            .And.Contain(pointer => pointer.Contains("/example", StringComparison.Ordinal));

        found.Should().OnlyContain(finding =>
            finding.Message.Contains("la forme de", StringComparison.Ordinal));

        foreach (Finding finding in found)
        {
            finding.Message.Should().NotContain(Password).And.NotContain(Token);
            finding.RecommendedAction.Should().NotContain(Password).And.NotContain(Token);
        }
    }

    [Fact]
    public async Task The_reports_name_the_rule_and_the_pointer_without_the_value()
    {
        AuditRunHarness harness = new(_root, "rapports", "secrets.yaml");

        await harness.RunAsync();

        string report = await harness.ReadArtifactAsync(ArtifactNames.AuditReport);
        string json = await harness.ReadArtifactAsync(ArtifactNames.DeltaJson);

        report.Should().Contain("AUD-306").And.NotContain(Password).And.NotContain(Token);
        json.Should().Contain("AUD-306").And.NotContain(Password).And.NotContain(Token);
    }

    [Fact]
    public async Task A_checkpoint_of_the_run_carries_nothing_from_the_document()
    {
        // Le point de reprise ne porte que la sélection, et la sélection ne peut atteindre aucun nœud.
        // L'invariant est structurel ; cet essai le constate sur le fichier le plus hostile du corpus.
        AuditRunHarness harness = new(_root, "reprise", "secrets.yaml");

        await harness.RunAsync();

        foreach (string path in Directory.EnumerateFiles(
            _root, "*.json", SearchOption.AllDirectories))
        {
            string content = await File.ReadAllTextAsync(path);

            content.Should().NotContain(Password).And.NotContain(Token);
        }
    }

    [Fact]
    public async Task A_secret_shaped_value_is_a_warning_and_never_a_remedy_the_tool_would_apply()
    {
        // Retirer une valeur d'exemple change ce que la description publie : l'outil le signale et n'y
        // touche pas, quelle que soit la gravité.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "secrets.yaml");

        AuditHarness.Of(audit, "AUD-306").Should().OnlyContain(finding =>
            finding.Safety == SafetyClass.Unsafe && !finding.AutofixAvailable);
    }

    [Fact]
    public async Task A_description_without_secrets_raises_nothing()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        AuditHarness.Of(audit, "AUD-306").Should().BeEmpty(
            "une reconnaissance large ne doit pas devenir du bruit sur un document ordinaire");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
