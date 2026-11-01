using System.Globalization;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation;

/// <summary>Export interactif d'un contrat SOAP WSDL vers OpenAPI YAML.</summary>
public sealed class WsdlOpenApiTool(IWsdlOpenApiExporter exporter, IAnsiConsole console) : ITool
{
    /// <inheritdoc />
    public string Id => "wsdl-openapi";

    /// <inheritdoc />
    public string DisplayName => "WSDL vers OpenAPI";

    /// <inheritdoc />
    public string Tab => "OpenAPI";

    /// <inheritdoc />
    public string Description => "Exporter un WSDL local ou une URL en OpenAPI, ou rejouer une collecte";

    /// <inheritdoc />
    public bool RequiresServer => false;

    /// <inheritdoc />
    public async Task<ToolRun> RunAsync(CancellationToken cancellationToken)
    {
        ToolRun run = new(Guid.NewGuid(), Id, DateTimeOffset.UtcNow, ["Export WSDL"]);
        StepState step = run.Steps[0];
        try
        {
            console.Clear();
            console.MarkupLine("[grey]Export SOAP document/literal. Les constructions non prises en charge sont signalées.[/]");
            string source = await console.AskAsync<string>("Fichier WSDL, manifest.json de collecte ou URL HTTPS :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            string target = await console.AskAsync<string>("Fichier YAML de sortie :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            string title = await console.AskAsync<string>("Titre OpenAPI :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            string version = await console.AskAsync<string>("Version OpenAPI du service :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            bool remote = Uri.TryCreate(source, UriKind.Absolute, out Uri? sourceUri) && !sourceUri.IsFile;
            string sourcePath = remote ? string.Empty : Path.GetFullPath(source);
            string targetPath = Path.GetFullPath(target);
            if (SamePath(sourcePath, targetPath))
            {
                throw new InvalidOperationException("Le fichier de sortie doit être distinct du WSDL source.");
            }

            if (File.Exists(targetPath))
            {
                throw new IOException("Le fichier de sortie existe déjà : aucun écrasement autorisé.");
            }

            step.Start(DateTimeOffset.UtcNow);
            (string yaml, string? collectedTarget) = await GenerateAsync(sourcePath, remote ? sourceUri : null,
                title, version, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (collectedTarget is null || !SamePath(collectedTarget, targetPath))
            {
                await PublishAsync(targetPath, yaml, cancellationToken).ConfigureAwait(false);
            }

            step.Complete(DateTimeOffset.UtcNow);
            run.End(RunOutcome.Success, DateTimeOffset.UtcNow);
            console.MarkupLine("[green]YAML OpenAPI généré sans écraser de fichier existant.[/]");
        }
        catch (OperationCanceledException)
        {
            run.End(RunOutcome.Cancelled, DateTimeOffset.UtcNow);
        }
        catch (WsdlExportException exception)
        {
            console.MarkupLine("[red]" + Markup.Escape(exception.Message) + "[/]");
            run.End(RunOutcome.Failed, DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or System.Xml.XmlException)
        {
            // Ne jamais afficher l'exception : elle peut contenir des chemins et URLs sensibles.
            console.MarkupLine("[red]Export refusé : entrée, destination ou contrat non exploitable.[/]");
            run.End(RunOutcome.Failed, DateTimeOffset.UtcNow);
        }

        return run;
    }

    private async Task<(string Yaml, string? CollectedTarget)> GenerateAsync(string local, Uri? remote,
        string title, string version, CancellationToken cancellationToken)
    {
        if (remote is null)
        {
            return (await exporter.ExportAsync(local, title, version, cancellationToken).ConfigureAwait(false), null);
        }

        WsdlUrlExportOptions options = await AskRemoteOptionsAsync(remote, cancellationToken).ConfigureAwait(false);
        string yaml = await exporter.ExportUrlAsync(remote, options, title, version, cancellationToken).ConfigureAwait(false);
        console.MarkupLine("[grey]Collecte conservée avec manifest.json, copies hors ligne et compatibility.json. L'import dans une instance Sparky reste à vérifier.[/]");
        return (yaml, Path.Combine(Path.GetFullPath(options.SnapshotDirectory), "openapi.yaml"));
    }

    private async Task<WsdlUrlExportOptions> AskRemoteOptionsAsync(Uri source, CancellationToken cancellationToken)
    {
        string folder = await console.AskAsync<string>("Nouveau dossier pour conserver la collecte :", CultureInfo.InvariantCulture,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        console.MarkupLine("[grey]Par défaut : même hôte uniquement, HTTPS, aucune adresse interne. La collecte permet de refaire l'export sans réseau.[/]");
        string hosts = await console.PromptAsync(new TextPrompt<string>("Autres hôtes d'import autorisés (virgules, vide si aucun) :")
            .AllowEmpty(), cancellationToken).ConfigureAwait(false);
        string addresses = await console.PromptAsync(new TextPrompt<string>("IP internes autorisées (virgules, vide si aucune) :")
            .AllowEmpty(), cancellationToken).ConfigureAwait(false);
        bool http = string.Equals(source.Scheme, "http", StringComparison.Ordinal)
            && await console.PromptAsync(new ConfirmationPrompt("Autoriser HTTP sans chiffrement pour cette collecte ?")
            { DefaultValue = false }, cancellationToken).ConfigureAwait(false);
        if (string.Equals(source.Scheme, "http", StringComparison.Ordinal) && !http)
        {
            throw new OperationCanceledException("Collecte HTTP annulée.", cancellationToken);
        }

        return new WsdlUrlExportOptions(folder, Split(hosts), Split(addresses), http);
    }

    private static async Task PublishAsync(string target, string yaml, CancellationToken cancellationToken)
    {
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, yaml, new System.Text.UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool SamePath(string first, string second) => string.Equals(first, second,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

}
