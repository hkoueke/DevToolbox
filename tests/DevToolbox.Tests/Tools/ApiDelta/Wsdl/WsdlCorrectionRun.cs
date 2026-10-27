using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// La chaîne entière, du contrat de service à la production corrigée, montée une fois.
/// </summary>
/// <remarks>
/// Les trois essais de la tranche regardent trois choses du même résultat : les traces, les marqueurs, et
/// l'octet intact de la production. Les monter trois fois ferait trois montages à dériver — et le jour où
/// l'un d'eux se tromperait, c'est l'essai qui mentirait, pas le code.
/// <para>
/// Le contrat est écrit sur le disque plutôt que lu depuis les ressources : la résolution d'imports est
/// confinée à une racine, et cette racine est un dossier. C'est le lecteur réel qui est éprouvé, avec sa
/// politique réelle, et non un lecteur qu'on aurait rendu plus commode pour l'occasion.
/// </para>
/// </remarks>
internal sealed class WsdlCorrectionRun : IDisposable
{
    private static readonly string[] Ordinary =
        ["catalogue.wsdl", "catalogue-types.xsd", "commun.xsd"];

    private readonly SpecReader _reader = new(NullLogger<SpecReader>.Instance);
    private readonly CorrectionBuilder _builder = new(
        new OperationCatalog(), new SplicingEmitter(new CanonicalEmitter()));

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-wsdl-correction-" + Guid.NewGuid().ToString("N"));

    private readonly string _entry;
    private readonly IReadOnlyList<string> _documents;

    private WsdlCorrectionRun(string entry, IReadOnlyList<string> documents)
    {
        _entry = entry;
        _documents = documents;

        Directory.CreateDirectory(_root);

        Contract = Read();
        Production = Production_();
        Projection = new Projector().Project(Contract, Production.DeclaredVersion);
        Differences = Compared();
        Footing = Established();
    }

    /// <summary>Le contrat, lu depuis le disque avec la politique réelle.</summary>
    public WsdlContract Contract { get; }

    /// <summary>Ce que la projection en a fait.</summary>
    public ProjectionResult Projection { get; }

    /// <summary>Le document synthétique, qui tient le rôle du candidat.</summary>
    public SpecDocument Candidate => Projection.Document;

    /// <summary>La production, celle que rien ne doit toucher.</summary>
    public SpecDocument Production { get; }

    /// <summary>Ce que la comparaison a trouvé entre les deux.</summary>
    public IReadOnlyList<Difference> Differences { get; }

    /// <summary>Ce sur quoi la sélection repose : dérivation, appariement, couverture.</summary>
    public WsdlFooting Footing { get; }

    /// <summary>Monte la chaîne sur la livraison ordinaire du corpus : trois documents.</summary>
    /// <returns>L'exécution, à libérer.</returns>
    public static WsdlCorrectionRun Establish() => new("catalogue.wsdl", Ordinary);

    /// <summary>Monte la chaîne sur une autre livraison du même contrat.</summary>
    /// <param name="entry">Le document d'entrée.</param>
    /// <param name="documents">Les documents à écrire, le point d'entrée compris.</param>
    /// <returns>L'exécution, à libérer.</returns>
    /// <remarks>
    /// Le point d'entrée et la liste des documents sont donnés à part : une livraison dont les schémas
    /// sont écrits dans le WSDL n'a qu'un seul fichier, et c'est précisément la différence qu'on éprouve.
    /// </remarks>
    public static WsdlCorrectionRun Delivered(string entry, params string[] documents) =>
        new(entry, documents);

    /// <summary>L'itinéraire qu'une opération a reçu de la dérivation.</summary>
    /// <param name="operation">Son nom.</param>
    /// <returns>La route, telle que la sélection la désigne.</returns>
    public OperationSelector Route(string operation)
    {
        DerivedRoute derived = Projection.Routes[operation];

        OperationSelector.TryParse(derived.Display, out OperationSelector? selector)
            .Should().BeTrue("« " + derived.Display + " » doit se relire comme une route");

        return selector!;
    }

    /// <summary>Le catalogue tel que l'écran de sélection le reçoit.</summary>
    /// <returns>Les routes du candidat, avec ce sur quoi les retenir reposerait.</returns>
    public IReadOnlyList<CorrectionRoute> Catalogue() => CorrectionCatalog.Build(
        Production, Candidate, Differences, new OperationCatalog(), Footing);

    /// <summary>Construit la production corrigée de ce qui a été retenu.</summary>
    /// <param name="selection">Ce qui est retenu.</param>
    /// <returns>Ce que le constructeur a produit.</returns>
    public Result<BuiltCorrection> Build(
        CorrectionSelection selection, bool includeRemovals = false)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return _builder.Build(Request(selection, includeRemovals));
    }

    /// <summary>Énonce ce qui serait inséré, sans rien écrire.</summary>
    /// <param name="selection">Ce qui est retenu.</param>
    /// <returns>Le plan.</returns>
    public CorrectionPlan Plan(CorrectionSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        Result<CorrectionPlan> plan = _builder.Plan(Request(selection));

        plan.IsSuccess.Should().BeTrue(plan.Failure?.Message);

        return plan.Value;
    }

    /// <summary>Vérifie la production corrigée, avec le vérificateur réel.</summary>
    /// <param name="built">Ce que le constructeur a produit.</param>
    /// <returns>Ce que la vérification a pu établir.</returns>
    public CorrectionVerification Verify(BuiltCorrection built)
    {
        ArgumentNullException.ThrowIfNull(built);

        return new CorrectionVerifier(_reader, new OpenApiSemanticAdapter(), new ReferenceGraph())
            .Verify(built.Serialized, Production, built, Ruleset.Default);
    }

    /// <summary>Relit des octets produits, comme la vérification le fait.</summary>
    /// <param name="bytes">Les octets.</param>
    /// <returns>Le document relu.</returns>
    public SpecDocument Reread(ReadOnlyMemory<byte> bytes)
    {
        SpecReadResult result = _reader.ReadBytes(
            bytes, "corrigé", DocumentRole.Production, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            "la production corrigée doit se relire : "
            + string.Join(" / ", result.Diagnostics.Select(one => one.Message)));

        return result.Document;
    }

    /// <summary>La demande complète, fermeture de dépendances comprise.</summary>
    /// <param name="selection">Ce qui est retenu.</param>
    /// <returns>La demande.</returns>
    public CorrectionRequest Request(CorrectionSelection selection, bool includeRemovals = false)
    {
        ArgumentNullException.ThrowIfNull(selection);

        Result<DependencyClosure> closure = new DependencyResolver(new OpenApiMetamodel()).Close(
            [.. Roots(selection)], Candidate, Production, CancellationToken.None);

        closure.IsSuccess.Should().BeTrue(closure.Failure?.Message);

        return new CorrectionRequest(
            Production,
            Candidate,
            Differences,
            selection,
            closure.Value,
            Version: null,
            CanonicalFormatting.Default,
            includeRemovals,
            Footing);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private IEnumerable<SpecPointer> Roots(CorrectionSelection selection)
    {
        foreach (OperationSelector route in selection.Routes)
        {
            yield return SpecPointer.Root.Child("paths").Child(route.Path).Child(route.MethodKey);
        }

        foreach (DifferenceSelector selector in selection.Differences)
        {
            Difference? match = Differences.FirstOrDefault(difference =>
                string.Equals(difference.ChangeId, selector.ChangeId, StringComparison.Ordinal)
                && string.Equals(difference.Location.Json, selector.At, StringComparison.Ordinal));

            if (match is not null)
            {
                yield return match.Location;
            }
        }
    }

    private WsdlContract Read()
    {
        foreach (string name in _documents)
        {
            File.WriteAllText(
                Path.Combine(_root, name),
                CorpusLoader.Read("Wsdl." + name),
                new UTF8Encoding(false));
        }

        WsdlReadResult read = new WsdlReader().ReadAsync(
            Path.Combine(_root, _entry),
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).GetAwaiter().GetResult();

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(one => one.Message)));

        return read.Contract!;
    }

    private SpecDocument Production_()
    {
        SpecReadResult result = _reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read("Wsdl.production.yaml")),
            "production.yaml",
            DocumentRole.Production,
            LoadLimits.Default);

        result.Document.Should().NotBeNull(
            "la production du corpus doit se lire : "
            + string.Join(" / ", result.Diagnostics.Select(one => one.Message)));

        return result.Document;
    }

    private IReadOnlyList<Difference> Compared()
    {
        Result<IReadOnlyList<Difference>> compared = new DocumentComparer(new OpenApiMetamodel())
            .Compare(Candidate, Production, CancellationToken.None);

        compared.IsSuccess.Should().BeTrue(compared.Failure?.Message);

        return compared.Value;
    }

    /// <summary>L'appariement proposé puis confirmé, et la couverture qui en découle.</summary>
    /// <remarks>
    /// Confirmé, parce que rien ne se compare avant confirmation : une base non confirmée rendrait la
    /// couverture non établie, et l'écran n'aurait rien à montrer. C'est ce que fait le développeur quand
    /// il valide l'appariement, et l'essai ne peut pas s'en dispenser.
    /// </remarks>
    private WsdlFooting Established()
    {
        IReadOnlyList<string> operations =
            [.. Contract.Operations.Select(operation => operation.Name)];

        SpecDocumentRoutes routes = SpecDocumentRoutes.From(Production);

        OperationCorrespondence confirmed = new SimilarityProposer()
            .Propose(operations, routes.Routes)
            .Confirm();

        return new WsdlFooting(
            DerivationIndex.From(Contract, Projection),
            confirmed,
            CoverageReport.Establish(
                confirmed, operations, new HashSet<string>(routes.Routes, StringComparer.Ordinal)));
    }
}
