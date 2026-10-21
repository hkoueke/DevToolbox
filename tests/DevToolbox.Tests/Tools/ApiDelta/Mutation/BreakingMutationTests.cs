using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Mutation;

/// <summary>
/// Chaque mutation ci-dessous resserre le contrat. Aucune ne doit passer pour compatible.
/// </summary>
/// <remarks>
/// C'est l'essai dont l'échec coûte le plus cher. Un changement cassant annoncé comme compatible se
/// découvre chez l'appelant, après publication, et l'outil aura servi à donner confiance en se trompant —
/// ce qui est pire que de n'avoir rien dit.
/// <para>
/// Les mutations sont appliquées au texte de la description de référence, une à une : ce qui est éprouvé
/// est donc le classement d'un changement isolé, et non la somme de plusieurs.
/// </para>
/// </remarks>
public sealed class BreakingMutationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-mutations-" + Guid.NewGuid().ToString("N"));

    public BreakingMutationTests() => Directory.CreateDirectory(_root);

    /// <summary>
    /// Les resserrements, chacun décrit par ce qu'il remplace dans la description de référence.
    /// </summary>
    public static TheoryData<string, string, string> Tightenings => new()
    {
        {
            "un paramètre devient obligatoire",
            "          required: false",
            "          required: true"
        },
        {
            "une borne supérieure est abaissée",
            "          maxLength: 256",
            "          maxLength: 64"
        },
        {
            "une borne inférieure est ajoutée",
            "          maxLength: 128",
            "          maxLength: 128\n          minLength: 10"
        },
        {
            "une valeur d'énumération disparaît",
            "            enum: [brouillon, publie, archive]",
            "            enum: [brouillon, publie]"
        },
        {
            // La borne reste : sans cela, la mutation porterait aussi un élargissement, et ce qui serait
            // éprouvé ne serait plus un changement isolé.
            "un type change",
            "        titre:\n          type: string\n          maxLength: 256",
            "        titre:\n          type: integer\n          maxLength: 256"
        },
        {
            "un format apparaît sur un champ qui n'en avait pas",
            "        libelle:\n          type: string\n          maxLength: 128",
            "        libelle:\n          type: string\n          format: uuid\n          maxLength: 128"
        },
        {
            "une propriété devient obligatoire",
            "      required: [titre]",
            "      required: [titre, dossierId]"
        },
        {
            // Une disparition pure. Renommer un code serait une disparition doublée d'une apparition, et
            // l'apparition d'une réponse est compatible : la mutation ne serait plus un resserrement.
            "une réponse disparaît",
            "        '401':\n          $ref: '#/components/responses/NonAutorise'\n",
            ""
        },
        {
            "un corps de requête devient obligatoire",
            "      operationId: modifierDocument\n      requestBody:",
            "      operationId: modifierDocument\n      requestBody:\n        required: true"
        },
        {
            "une exigence de sécurité est ajoutée à une opération qui s'en passait",
            "      operationId: listerDossiers\n      security: []",
            "      operationId: listerDossiers\n      security:\n        - bearerAuth: []"
        },
    };

    /// <summary>
    /// Les élargissements, qu'aucun ne doit passer pour cassant.
    /// </summary>
    /// <remarks>
    /// L'erreur symétrique, et moins coûteuse mais plus usante : un outil qui crie au changement cassant
    /// à chaque élargissement finit par n'être plus lu, et c'est alors le vrai changement cassant qui
    /// passe.
    /// </remarks>
    public static TheoryData<string, string, string> Loosenings => new()
    {
        {
            "une borne supérieure est relevée",
            "          maxLength: 256",
            "          maxLength: 512"
        },
        {
            "un champ se met à accepter l'absence de valeur",
            "        libelle:\n          type: string\n          maxLength: 128",
            "        libelle:\n          type: string\n          nullable: true\n          maxLength: 128"
        },
    };

    /// <summary>
    /// La nullabilité a la polarité inverse de l'obligation, et les deux ne partagent pas de règle.
    /// </summary>
    /// <remarks>
    /// L'essai compare deux mutations de la même description plutôt qu'une mutation contre l'original :
    /// c'est le seul moyen d'éprouver un <em>passage</em> de <c>true</c> à <c>false</c>, que la référence
    /// ne porte pas. Ajouter <c>nullable: false</c> à un champ qui n'en avait pas ne resserre rien — en
    /// OpenAPI 3.0 c'est le défaut — et ne figure donc pas parmi les resserrements.
    /// </remarks>
    [Fact]
    public async Task Losing_the_permission_to_be_null_is_breaking_and_gaining_it_is_not()
    {
        const string Permissive = "        libelle:\n          type: string\n          nullable: true";
        const string Strict = "        libelle:\n          type: string\n          nullable: false";
        const string Plain = "        libelle:\n          type: string";

        IReadOnlyList<Difference> tightened = await CompareAsync(
            production: Replace(Plain, Permissive), candidate: Replace(Plain, Strict));

        tightened.Should().Contain(difference => difference.Impact == ChangeImpact.Breaking);

        IReadOnlyList<Difference> loosened = await CompareAsync(
            production: Replace(Plain, Strict), candidate: Replace(Plain, Permissive));

        loosened.Should().NotBeEmpty();
        loosened.Should().NotContain(difference => difference.Impact == ChangeImpact.Breaking);
    }

    /// <summary>
    /// Une valeur d'énumération ajoutée est affaire de politique, et majeure par défaut.
    /// </summary>
    /// <remarks>
    /// FR-304 : là où l'effet d'un changement dépend d'une décision plutôt que de la spécification, le
    /// classement est configurable. Ajouter une valeur n'empêche aucun appelant d'émettre ce qu'il
    /// émettait, mais un appelant qui <em>reçoit</em> la nouvelle valeur peut ne pas la traiter. L'outil
    /// retient le choix prudent par défaut et laisse l'équipe en décider autrement.
    /// </remarks>
    [Fact]
    public async Task An_added_enum_value_is_a_matter_of_policy_and_major_by_default()
    {
        IReadOnlyList<Difference> differences = await CompareMutatedAsync(
            "une valeur d'énumération est ajoutée",
            "            enum: [brouillon, publie, archive]",
            "            enum: [brouillon, publie, archive, supprime]");

        differences.Should().Contain(difference =>
            difference.Impact == ChangeImpact.Breaking,
            "la politique livrée tient une valeur ajoutée pour majeure");

        differences.Should().Contain(difference => difference.Policy != null,
            "un classement qui dépend d'une politique doit nommer la politique qui l'a décidé");
    }

    [Theory]
    [MemberData(nameof(Tightenings))]
    public async Task A_tightening_is_never_called_compatible(string description, string from, string to)
    {
        IReadOnlyList<Difference> differences = await CompareMutatedAsync(description, from, to);

        differences.Should().NotBeEmpty("la mutation « {0} » doit se voir", description);

        differences.Should().NotContain(
            difference => difference.Impact == ChangeImpact.NonBreaking,
            "« {0} » resserre le contrat : rien n'y est compatible",
            description);

        differences.Should().NotContain(
            difference => difference.Impact == ChangeImpact.DocumentationOnly,
            "« {0} » n'est pas de la documentation",
            description);
    }

    [Theory]
    [MemberData(nameof(Tightenings))]
    public async Task A_tightening_is_either_breaking_or_openly_unknown(
        string description,
        string from,
        string to)
    {
        IReadOnlyList<Difference> differences = await CompareMutatedAsync(description, from, to);

        // Cassant, ou inconnu annoncé comme tel. Les deux sont honnêtes ; « compatible » ne l'est pas.
        differences.Should().OnlyContain(difference =>
            difference.Impact == ChangeImpact.Breaking || difference.Impact == ChangeImpact.Unknown);

        foreach (Difference difference in differences.Where(
            difference => difference.Impact == ChangeImpact.Unknown))
        {
            difference.Rationale.Should().NotBeEmpty(
                "un doute doit dire pourquoi il en est un");
        }
    }

    [Theory]
    [MemberData(nameof(Loosenings))]
    public async Task A_loosening_is_never_called_breaking(string description, string from, string to)
    {
        // Le symétrique : élargir ne casse personne, et le dire cassant ferait publier une version majeure
        // pour rien — ce qui apprend à l'équipe à ne plus croire l'outil.
        IReadOnlyList<Difference> differences = await CompareMutatedAsync(description, from, to);

        differences.Should().NotBeEmpty();
        differences.Should().NotContain(difference => difference.Impact == ChangeImpact.Breaking);
    }

    [Fact]
    public async Task A_parameter_that_becomes_optional_is_not_called_breaking()
    {
        IReadOnlyList<Difference> differences = await CompareMutatedAsync(
            "un en-tête obligatoire devient facultatif",
            "      required: true\n      description: Le service appelant.",
            "      required: false\n      description: Le service appelant.");

        differences.Should().NotBeEmpty();
        differences.Should().OnlyContain(difference => difference.Impact == ChangeImpact.NonBreaking);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>La description de référence, une mutation appliquée.</summary>
    private static string Replace(string from, string to)
    {
        string baseline = CorpusLoader.Read("baseline-30.yaml");

        baseline.Should().Contain(from, "« {0} » doit avoir prise sur la référence", from);

        return ReplaceFirst(baseline, from, to);
    }

    /// <summary>
    /// Compare deux textes, pour les cas qu'une seule mutation ne peut pas exprimer.
    /// </summary>
    private async Task<IReadOnlyList<Difference>> CompareAsync(string production, string candidate)
    {
        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        SpecReadResult read = await reader.ReadAsync(
            Write("mutant.yaml", candidate),
            DocumentRole.Candidate,
            LoadLimits.Default,
            CancellationToken.None);

        SpecReadResult reference = await reader.ReadAsync(
            Write("reference.yaml", production),
            DocumentRole.Production,
            LoadLimits.Default,
            CancellationToken.None);

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return new DocumentComparer(new OpenApiMetamodel())
            .Compare(read.Document!, reference.Document!, CancellationToken.None)
            .Value;
    }

    private async Task<IReadOnlyList<Difference>> CompareMutatedAsync(
        string description,
        string from,
        string to)
    {
        string baseline = CorpusLoader.Read("baseline-30.yaml");

        baseline.Should().Contain(
            from, "la mutation « {0} » doit avoir prise sur la description de référence", description);

        string mutated = ReplaceFirst(baseline, from, to);

        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        SpecReadResult candidate = await reader.ReadAsync(
            Write("mutant.yaml", mutated),
            DocumentRole.Candidate,
            LoadLimits.Default,
            CancellationToken.None);

        SpecReadResult production = await reader.ReadAsync(
            Write("reference.yaml", baseline),
            DocumentRole.Production,
            LoadLimits.Default,
            CancellationToken.None);

        candidate.IsFaithful.Should().BeTrue(
            string.Join(" · ", candidate.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return new DocumentComparer(new OpenApiMetamodel())
            .Compare(candidate.Document!, production.Document!, CancellationToken.None)
            .Value;
    }

    /// <summary>
    /// Remplace la première occurrence seulement : une mutation doit porter sur un endroit, afin que ce
    /// qui est classé soit un changement isolé.
    /// </summary>
    private static string ReplaceFirst(string content, string from, string to)
    {
        int position = content.IndexOf(from, StringComparison.Ordinal);

        return content.Remove(position, from.Length).Insert(position, to);
    }

    private string Write(string fileName, string content)
    {
        string path = Path.Combine(_root, fileName);
        File.WriteAllText(path, content);

        return path;
    }
}
