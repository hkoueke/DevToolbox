using DevToolbox.Tools.ApiDelta.Core.Audit;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Audit;

/// <summary>
/// Compare le catalogue des règles à ce que la spécification annonce, ligne à ligne.
/// </summary>
/// <remarks>
/// La spécification est lue sur le disque, et non recopiée ici : une transcription qu'il faut tenir à jour
/// à la main finirait par diverger sans que rien ne le dise, ce qui est exactement le défaut que cet essai
/// existe pour empêcher.
/// <para>
/// La sûreté portée par le catalogue est la meilleure que le remède puisse atteindre. Quand la
/// spécification dit « SAFE une fois telle condition remplie, sinon REVIEW_REQUIRED », c'est le premier
/// des deux qui est comparé, et la règle dégrade au vu du document.
/// </para>
/// </remarks>
public sealed class RuleCatalogueTests
{
    [Fact]
    public void The_catalogue_matches_the_specification_row_by_row()
    {
        Dictionary<string, (FindingSeverity Severity, SafetyClass Safety)> announced = Announced();

        announced.Should().NotBeEmpty("la spécification doit porter le catalogue des règles");

        RuleDefaults.All.Keys.OrderBy(id => id, StringComparer.Ordinal)
            .Should().Equal(announced.Keys.OrderBy(id => id, StringComparer.Ordinal));

        foreach ((string ruleId, (FindingSeverity severity, SafetyClass safety)) in announced)
        {
            RuleDefault row = RuleDefaults.For(ruleId);

            row.Severity.Should().Be(severity, "la gravité annoncée pour {0}", ruleId);
            row.Safety.Should().Be(safety, "la sûreté annoncée pour {0}", ruleId);
        }
    }

    /// <summary>Ce que la spécification annonce, règle par règle.</summary>
    private static Dictionary<string, (FindingSeverity Severity, SafetyClass Safety)> Announced()
    {
        Dictionary<string, (FindingSeverity Severity, SafetyClass Safety)> announced =
            new(StringComparer.Ordinal);

        foreach (string line in File.ReadLines(Specification()))
        {
            if (!line.StartsWith("| AUD-", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = line.Split('|', StringSplitOptions.TrimEntries);

            if (cells.Length < 5)
            {
                continue;
            }

            announced[cells[1]] = (Severity(cells[3]), Safety(cells[4]));
        }

        return announced;
    }

    /// <summary>La première gravité nommée par une cellule.</summary>
    /// <remarks>
    /// La première, parce qu'une règle peut en porter deux — « error pour un doublon, warning pour un
    /// manque ». Le catalogue porte alors celle du cas principal, et la règle choisit par constat.
    /// </remarks>
    private static FindingSeverity Severity(string cell) => First(
        cell,
        [
            ("error", FindingSeverity.Error),
            ("warning", FindingSeverity.Warning),
            ("information", FindingSeverity.Information),
            ("opportunity", FindingSeverity.Opportunity),
        ]);

    /// <summary>La première classe de sûreté nommée par une cellule.</summary>
    private static SafetyClass Safety(string cell) => First(
        cell,
        [
            ("SAFE", SafetyClass.Safe),
            ("REVIEW_REQUIRED", SafetyClass.ReviewRequired),
            ("report-only", SafetyClass.Unsafe),
        ]);

    /// <summary>Le premier mot reconnu dans une cellule, par position.</summary>
    private static T First<T>(string cell, IReadOnlyList<(string Word, T Value)> words)
    {
        (string Word, T Value) found = words
            .Select(candidate => (candidate, Position: cell.IndexOf(
                candidate.Word, StringComparison.Ordinal)))
            .Where(pair => pair.Position >= 0)
            .OrderBy(pair => pair.Position)
            .Select(pair => pair.candidate)
            .FirstOrDefault();

        found.Word.Should().NotBeNull("la cellule « {0} » doit nommer une valeur connue", cell);

        return found.Value;
    }

    /// <summary>
    /// Le fichier de spécification, trouvé en remontant jusqu'à la solution.
    /// </summary>
    /// <remarks>
    /// Remonter plutôt que coder un chemin : l'essai tourne depuis le dossier de compilation, dont la
    /// profondeur n'est pas la même selon la configuration.
    /// </remarks>
    private static string Specification()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DevToolbox.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("l'essai doit tourner dans l'arborescence du dépôt");

        string path = Path.Combine(
            directory.FullName, "specs", "003-openapi-delta-audit", "spec.md");

        File.Exists(path).Should().BeTrue("la spécification doit se trouver à {0}", path);

        return path;
    }
}
