namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Ce que l'application d'un plan a produit, et ce qu'elle a refusé de produire.</summary>
/// <param name="Serialized">Les octets de la copie optimisée, tels qu'ils seraient écrits.</param>
/// <param name="Applied">Les transformations appliquées.</param>
/// <param name="Declined">Les transformations écartées ou empêchées, chacune avec sa raison.</param>
/// <param name="Verification">Ce que la vérification a pu établir.</param>
/// <remarks>
/// Les écartées sont rendues, et non silencieusement omises. Une transformation que le plan proposait et
/// que l'application n'a pas faite est un fait que le développeur doit pouvoir lire : sans cela, un plan
/// de quatre propositions et une copie qui n'en porte qu'une se présenteraient de la même façon.
/// </remarks>
public sealed record OptimizationOutcome(
    ReadOnlyMemory<byte> Serialized,
    IReadOnlyList<Transformation> Applied,
    IReadOnlyList<DeclinedTransformation> Declined,
    VerificationResult Verification)
{
    /// <summary>Une application qui n'a rien produit.</summary>
    public static OptimizationOutcome None { get; } =
        new(ReadOnlyMemory<byte>.Empty, [], [], VerificationResult.NotRun);

    /// <summary>Indique si la copie peut être publiée.</summary>
    public bool Publishable => Applied.Count > 0 && Verification.Established;
}
