using DevToolbox.Infrastructure.Logging;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Infrastructure;
using VarCompareLog = DevToolbox.Tools.VarCompare.Infrastructure.Internal.Log;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Les secrets ne doivent atteindre un puits de journalisation par aucune des deux voies possibles.
/// </summary>
/// <remarks>
/// L'application est écrite de façon qu'aucune valeur ne soit jamais passée à un journal. Ces tests couvrent
/// les deux lignes de défense : que la discipline tienne, et que le puits rattrape la faute lorsqu'elle est
/// rompue.
/// </remarks>
public sealed class LoggingPrivacyTests
{
    private const string SecretValue = "sk-live-must-never-be-logged-0123456789";

    [Fact]
    public void The_log_methods_the_application_actually_uses_cannot_carry_a_value()
    {
        // Première ligne de défense, vérifiée structurellement : aucune méthode de journalisation générée
        // n'accepte un paramètre d'un type susceptible de porter une valeur de variable.
        Type log = typeof(VariableGroupGateway).Assembly
            .GetType("DevToolbox.Tools.VarCompare.Infrastructure.Internal.Log")!;

        IEnumerable<Type> parameterTypes = log
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType);

        parameterTypes.Should().NotContain(
            [typeof(VariableEntry), typeof(VariableGroupSnapshot)],
            "no log method may take a type that can hold a variable value");
    }

    [Fact]
    public void A_value_passed_to_a_logger_by_mistake_is_redacted_before_it_reaches_the_sink()
    {
        // Seconde ligne de défense, et la raison de son existence. Ce test fait délibérément la mauvaise
        // chose — passer une VariableEntry directement à ILogger — parce qu'un garde-fou qui n'est éprouvé
        // que par du code correct n'est pas un garde-fou.
        using TempLogRoot temp = new();

        RedactionRegistry registry = new();
        ServiceCollectionExtensions.RegisterRedactedTypes(registry);

        FileLoggerProvider file = new(temp.Paths, new FileLoggerOptions { RetentionDays = 7 });
        using RedactingLoggerProvider provider = new(file, registry);

        ILogger logger = provider.CreateLogger("test");

        VariableEntry leaky = new("Api__Key", SecretValue, IsSecret: true, IsReadOnly: false);
#pragma warning disable CA2254, CA1873 // Mauvais usage délibéré : tout l'intérêt est d'enfreindre la règle.
        logger.LogInformation("Leaking {Entry}", leaky);
#pragma warning restore CA2254, CA1873

        string written = ReadAllLogs(temp);

        written.Should().NotContain(SecretValue);
        written.Should().Contain("[redacted:VariableEntry]");
        written.Should().Contain("redacted at the sink boundary");
    }

    [Fact]
    public void A_snapshot_passed_to_a_logger_by_mistake_is_also_redacted()
    {
        using TempLogRoot temp = new();

        RedactionRegistry registry = new();
        ServiceCollectionExtensions.RegisterRedactedTypes(registry);

        FileLoggerProvider file = new(temp.Paths, new FileLoggerOptions { RetentionDays = 7 });
        using RedactingLoggerProvider provider = new(file, registry);

        VariableGroupSnapshot snapshot = new(
            new VariableGroupSummary(1, "app-prod", null, VariableGroupOrigin.Ordinary, false),
            [new VariableEntry("Api__Key", SecretValue, false, false)],
            DateTimeOffset.UnixEpoch);

#pragma warning disable CA2254, CA1873 // Mauvais usage délibéré, sous test.
        provider.CreateLogger("test").LogInformation("Leaking {Snapshot}", snapshot);
#pragma warning restore CA2254, CA1873

        ReadAllLogs(temp).Should().NotContain(SecretValue);
    }

    [Fact]
    public void Ordinary_log_arguments_pass_through_untouched()
    {
        // Le masquage ne doit pas être si large qu'il détruise la trace qu'il protège.
        using TempLogRoot temp = new();

        RedactionRegistry registry = new();
        ServiceCollectionExtensions.RegisterRedactedTypes(registry);

        FileLoggerProvider file = new(temp.Paths, new FileLoggerOptions { RetentionDays = 7 });
        using RedactingLoggerProvider provider = new(file, registry);

#pragma warning disable CA1873 // Un test, pas un chemin critique.
        provider.CreateLogger("test").LogInformation(
            "Read group {GroupName} in {Collection}", "app-prod", "DefaultCollection");
#pragma warning restore CA1873

        string written = ReadAllLogs(temp);

        written.Should().Contain("app-prod");
        written.Should().Contain("DefaultCollection");
        written.Should().NotContain("redacted");
    }

    [Fact]
    public void A_run_trace_lets_an_observer_reconstruct_which_groups_were_read()
    {
        // La trace doit permettre de reconstituer l'exécution, pas seulement être exempte de secrets.
        RecordingLogger logger = new();

        VarCompareLog.RunStarted(
            logger, "1.0.0", "DefaultCollection");
        VarCompareLog.GroupRetrieved(
            logger, "app-dev", 1, "DefaultCollection", "MyProject", "Success");
        VarCompareLog.GroupRetrieved(
            logger, "app-prod", 2, "DefaultCollection", "MyProject", "Success");
        VarCompareLog.ComparisonBuilt(logger, 42, 2);

        string trace = logger.AllText;

        trace.Should().Contain("app-dev");
        trace.Should().Contain("app-prod");
        trace.Should().Contain("MyProject");
        trace.Should().Contain("42");
        trace.Should().NotContain(SecretValue);
    }

    private static string ReadAllLogs(TempLogRoot temp) =>
        string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(temp.Paths.LogsDirectory, "*.log")
                .Select(File.ReadAllText));

    /// <summary>Dirige le dossier des journaux vers un répertoire temporaire, le temps d'un test.</summary>
    private sealed class TempLogRoot : IDisposable
    {
        private readonly string _root;

        internal TempLogRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "devtoolbox-logs", Guid.NewGuid().ToString("N"));

            Paths = new AppPaths(_root, _root);
            Paths.EnsureCreated();
        }

        internal AppPaths Paths { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
