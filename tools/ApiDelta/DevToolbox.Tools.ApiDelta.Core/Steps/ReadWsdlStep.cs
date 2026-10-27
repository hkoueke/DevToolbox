using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>Lit le candidat lorsqu'il s'agit d'un contrat de service WSDL.</summary>
/// <remarks>
/// Un WSDL est un <em>format d'entrée candidat</em> de plus, et non un outil parallèle. L'étape se place
/// donc avant la lecture OpenAPI du candidat et ne fait rien quand le candidat n'en est pas un : la suite
/// de la séance — comparaison, choix, correction, écriture — est exactement la même dans les deux cas,
/// ce qui est la raison d'être de cette conception.
/// <para>
/// La détection se fait sur le contenu. Choisir la façon de lire un document d'après son extension
/// revient à croire ce que son nom affirme plutôt que ce que le document est, et un contrat servi sous
/// « .xml » est courant.
/// </para>
/// <para>
/// Un candidat donné par adresse est récupéré puis <em>matérialisé</em>, et c'est la seule différence
/// entre les deux chemins. Tout ce qui suit travaille sur des fichiers, si bien qu'aucune règle déjà en
/// place n'a besoin d'une exception pour une entrée venue du réseau.
/// </para>
/// </remarks>
public sealed class ReadWsdlStep : IToolStep
{
    private readonly IWsdlReader _reader;
    private readonly IContractFetcher _fetcher;

    /// <summary>Crée l'étape.</summary>
    /// <param name="reader">Lit un contrat depuis le disque.</param>
    /// <param name="fetcher">Récupère un contrat depuis une adresse.</param>
    public ReadWsdlStep(IWsdlReader reader, IContractFetcher fetcher)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(fetcher);

        _reader = reader;
        _fetcher = fetcher;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.ReadWsdl;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);

        if (selection?.CandidatePath is not { } candidate)
        {
            // Pas de candidat : un audit, une optimisation ou une extraction. Rien à faire, et surtout
            // pas un échec.
            return Result.Success();
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? address) && !address.IsFile
            ? await FetchAsync(context, selection, address, cancellationToken).ConfigureAwait(false)
            : await ReadAsync(context, candidate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lit un candidat donné par chemin, s'il s'agit bien d'un WSDL.</summary>
    private async Task<Result> ReadAsync(
        ToolRunContext context,
        string candidate,
        CancellationToken cancellationToken)
    {
        if (!await _reader.IsWsdlAsync(candidate, cancellationToken).ConfigureAwait(false))
        {
            // Le candidat est une description OpenAPI. La lecture suivante s'en charge, et cette étape
            // n'a rien à dire.
            return Result.Success();
        }

        WsdlReadResult read = await _reader.ReadAsync(
            candidate,
            WsdlLimits.Default,
            Confined(candidate),
            cancellationToken).ConfigureAwait(false);

        DiagnosticCollector.Add(context, read.Diagnostics);

        if (!read.IsFaithful)
        {
            return Result.Fail(
                FailureReason.InvalidInput,
                "Le contrat de service n'a pas pu être lu fidèlement : "
                    + DiagnosticCollector.Summarise(
                        [.. read.Diagnostics.Where(diagnostic => diagnostic.IsBlocking)]));
        }

        context.Set(ApiDeltaContextKeys.WsdlContract, read.Contract!);

        return Result.Success();
    }

    /// <summary>Récupère un candidat donné par adresse, puis le lit depuis les fichiers écrits.</summary>
    private async Task<Result> FetchAsync(
        ToolRunContext context,
        RunSelection selection,
        Uri address,
        CancellationToken cancellationToken)
    {
        string directory = Path.Combine(selection.OutputDirectory, "contrat-recupere");

        Result<WsdlContract> fetched = await _fetcher.FetchAsync(
            address,
            directory,
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(directory) with { AllowRemote = true, AllowedHosts = [address.Host] },
            cancellationToken).ConfigureAwait(false);

        if (!fetched.IsSuccess)
        {
            return Result.Fail(fetched.Failure!);
        }

        context.Set(ApiDeltaContextKeys.WsdlContract, fetched.Value);

        return Result.Success();
    }

    /// <summary>
    /// La politique d'un candidat donné par chemin : confinée au dossier qui le contient.
    /// </summary>
    /// <remarks>
    /// Au dossier du candidat, et non au dossier de sortie : un WSDL dont les types vivent dans un schéma
    /// voisin est le cas ordinaire, et confiner ailleurs refuserait la forme la plus courante. Ce que ce
    /// confinement empêche reste l'essentiel — un import qui remonterait pour lire ailleurs sur le
    /// disque — et le périmètre retenu est annoncé par le contrat lui-même.
    /// </remarks>
    private static ImportPolicy Confined(string candidate) =>
        ImportPolicy.ConfinedTo(Path.GetDirectoryName(Path.GetFullPath(candidate)) ?? ".");
}
