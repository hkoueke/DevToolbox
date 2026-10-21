using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Rendering;

/// <summary>
/// Montre ce que l'extraction a produit : ce qui a été pris, ce que chacune a emporté, et le verdict.
/// </summary>
/// <remarks>
/// L'héritage est affiché opération par opération parce que c'est ce qu'un développeur ne peut pas
/// vérifier en ouvrant l'extrait : un paramètre venu du niveau du chemin y est indiscernable d'un
/// paramètre qui y était déjà. Le dire est ce qui lui permet de constater que l'extrait n'a rien perdu de
/// ce qui protégeait l'opération.
/// </remarks>
public sealed class ExtractView
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée la vue.</summary>
    /// <param name="console">La console où écrire.</param>
    public ExtractView(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Affiche le résultat de l'extraction.</summary>
    /// <param name="result">Ce que l'extraction a produit.</param>
    /// <param name="artifacts">Les artefacts écrits.</param>
    public void Show(ExtractResult result, IReadOnlyList<Artifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(artifacts);

        RenderSummary(result);
        RenderInheritance(result);
        RenderDangling(result);
        RenderVerdict(result, artifacts);
    }

    private void RenderSummary(ExtractResult result)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow("[grey]opérations prises[/]", Count(result.InheritedContext.Count));
        grid.AddRow("[grey]composants emportés[/]", Count(result.Closure.Required.Count));
        grid.AddRow(
            "[grey]étiquettes emportées[/]",
            result.CarriedTags.Count == 0
                ? "[grey]aucune[/]"
                : Markup.Escape(string.Join(", ", result.CarriedTags)));
        grid.AddRow("[grey]tiré de[/]", Markup.Escape(Path.GetFileName(result.Provenance.SourcePath)));

        _console.Write(new Panel(grid)
            .Header("[bold]Ce que l'extrait contient[/]")
            .Border(BoxBorder.Rounded));

        _console.MarkupLine("[grey]Le fichier source n'a pas été modifié.[/]");
        _console.WriteLine();
    }

    /// <summary>Ce que chaque opération a hérité de son contexte.</summary>
    private void RenderInheritance(ExtractResult result)
    {
        if (result.InheritedContext.Count == 0)
        {
            return;
        }

        Table table = new();
        table.AddColumns("Opération", "Paramètres hérités", "Serveurs", "Sécurité");

        foreach (InheritanceRecord record in result.InheritedContext)
        {
            table.AddRow(
                Markup.Escape(record.Selector.Display),
                Listed(record.PathParameters),
                Listed(record.Servers),
                Listed(record.Security));
        }

        _console.Write(table);
        _console.WriteLine();
    }

    /// <summary>
    /// Les extensions emportées qui nomment ce que l'extrait ne contient pas.
    /// </summary>
    /// <remarks>
    /// Dit, jamais corrigé. L'outil ne connaît pas la grammaire de ces extensions, et les ajuster serait
    /// réécrire une donnée dont il ignore le sens.
    /// </remarks>
    private void RenderDangling(ExtractResult result)
    {
        foreach (Finding dangling in result.DanglingExtensionReferences)
        {
            _console.MarkupLine(
                "[yellow]Extension à vérifier :[/] " + Markup.Escape(dangling.Message));
        }

        if (result.DanglingExtensionReferences.Count > 0)
        {
            _console.WriteLine();
        }
    }

    /// <summary>Le verdict, et le fichier écrit — ou l'absence de fichier.</summary>
    private void RenderVerdict(ExtractResult result, IReadOnlyList<Artifact> artifacts)
    {
        if (!result.Verification.Established)
        {
            _console.MarkupLine(
                "[red]Aucun fichier n'a été écrit :[/] [grey]"
                + Markup.Escape(result.Verification.Explanation) + "[/]");

            return;
        }

        _console.MarkupLine(
            "[green]Vérifié :[/] [grey]" + Markup.Escape(result.Verification.Explanation) + "[/]");

        foreach (Artifact artifact in artifacts.Where(
            artifact => artifact.Kind == ArtifactKind.Extract))
        {
            _console.MarkupLine(
                "[grey]écrit :[/] " + Markup.Escape(artifact.Path)
                + " [grey](" + Count((int)artifact.ByteLength) + " octets)[/]");
        }
    }

    private static string Listed(IReadOnlyList<string> words) =>
        words.Count == 0 ? "—" : Markup.Escape(string.Join(", ", words));

    private static string Count(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
