using System.Globalization;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
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
    public string Description => "Convertir un contrat SOAP local en YAML OpenAPI 3.0.3";

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
            string source = await console.AskAsync<string>("Fichier WSDL local :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            string target = await console.AskAsync<string>("Fichier YAML de sortie :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            string title = await console.AskAsync<string>("Titre OpenAPI :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            string version = await console.AskAsync<string>("Version OpenAPI du service :", CultureInfo.InvariantCulture, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            string sourcePath = Path.GetFullPath(source);
            string targetPath = Path.GetFullPath(target);
            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Le fichier de sortie doit être distinct du WSDL source.");
            }

            if (File.Exists(targetPath))
            {
                throw new IOException("Le fichier de sortie existe déjà : aucun écrasement autorisé.");
            }

            step.Start(DateTimeOffset.UtcNow);
            string yaml = await exporter.ExportAsync(
                sourcePath, title, version, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            FileStream output = new(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using (output.ConfigureAwait(false))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(yaml);
                await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }

            step.Complete(DateTimeOffset.UtcNow);
            run.End(RunOutcome.Success, DateTimeOffset.UtcNow);
            console.MarkupLine("[green]YAML OpenAPI généré sans écraser de fichier existant.[/]");
        }
        catch (OperationCanceledException)
        {
            run.End(RunOutcome.Cancelled, DateTimeOffset.UtcNow);
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
}
