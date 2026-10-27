using DevToolbox.Tools.ApiDelta.Core.Loading;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Rendering;

/// <summary>
/// Montre ce que la lecture et la validation ont trouvé à dire, et où.
/// </summary>
/// <remarks>
/// Une ligne et une colonne par diagnostic, et les autres emplacements qui comptent juste en dessous : les
/// deux moitiés d'une clé en double, chacun des usages d'une référence morte. Nommer un seul endroit
/// obligerait le développeur à chercher les autres lui-même, alors que l'outil les connaît.
/// <para>
/// La gravité est portée par un mot autant que par une couleur : un terminal sans couleur doit rester
/// lisible.
/// </para>
/// </remarks>
public sealed class DiagnosticsView
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée la vue.</summary>
    /// <param name="console">La console où écrire.</param>
    public DiagnosticsView(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>
    /// Affiche des diagnostics, groupés par fichier, les plus graves d'abord.
    /// </summary>
    /// <param name="diagnostics">Ce qu'il y a à dire.</param>
    /// <param name="path">Le fichier auquel rattacher ce qui n'en nomme aucun.</param>
    /// <remarks>
    /// Groupés par le fichier que chaque diagnostic nomme, et non tous sous un seul en-tête. L'en-tête
    /// unique était celui de la production : une plainte portant sur le fichier compagnon envoyait donc
    /// chercher un défaut dans un fichier parfaitement sain, et c'est arrivé.
    /// <para>
    /// Ce qui ne nomme aucun fichier reste rattaché à celui qu'on passe ici — un refus de configuration,
    /// par exemple, qui porte déjà son fichier dans son message.
    /// </para>
    /// </remarks>
    public void Render(IReadOnlyList<Diagnostic> diagnostics, string path)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (diagnostics.Count == 0)
        {
            return;
        }

        foreach (IGrouping<string, Diagnostic> document in diagnostics
            .GroupBy(diagnostic => diagnostic.Document ?? path, StringComparer.Ordinal)
            .OrderBy(group => Worst(group), Comparer<DiagnosticSeverity>.Default)
            .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            _console.Write(
                new Rule("[bold]" + Markup.Escape(Path.GetFileName(document.Key)) + "[/]")
                    .LeftJustified());

            foreach (Diagnostic diagnostic in document
                .OrderBy(diagnostic => diagnostic.Severity)
                .ThenBy(diagnostic => diagnostic.Position?.Line ?? 0))
            {
                Render(diagnostic);
            }

            _console.WriteLine();
        }
    }

    /// <summary>La pire gravité d'un groupe, pour que le fichier le plus en cause passe en premier.</summary>
    private static DiagnosticSeverity Worst(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.Min(diagnostic => diagnostic.Severity);

    private void Render(Diagnostic diagnostic)
    {
        (string colour, string word) = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => ("red", "erreur"),
            DiagnosticSeverity.Warning => ("yellow", "avertissement"),
            _ => ("grey", "information"),
        };

        string place = diagnostic.Position is { } position
            ? " [grey](" + position.ToString() + ")[/]"
            : string.Empty;

        _console.MarkupLine(
            $"[{colour}]{word}[/] [grey]{Markup.Escape(diagnostic.Code)}[/]{place} "
            + Markup.Escape(diagnostic.Message));

        if (diagnostic.Location is { } location && location.Display.Length > 0)
        {
            _console.MarkupLine("  [grey]" + Markup.Escape(location.Display) + "[/]");
        }

        foreach (Core.Syntax.SourcePosition related in diagnostic.Related)
        {
            _console.MarkupLine("  [grey]également " + Markup.Escape(related.ToString()) + "[/]");
        }
    }
}
