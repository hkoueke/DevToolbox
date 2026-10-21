namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Le nom publié de chaque sorte de candidat.</summary>
/// <remarks>
/// Publié, donc fixé ici et non dérivé du nom du membre d'énumération : renommer un membre ne doit pas
/// renommer un champ que des consommateurs lisent.
/// </remarks>
public static class CandidateKindNames
{
    /// <summary>Le nom publié d'une sorte.</summary>
    /// <param name="kind">La sorte.</param>
    /// <returns>Le nom, tel qu'il apparaît dans les artefacts.</returns>
    public static string Of(CandidateKind kind) => kind switch
    {
        CandidateKind.Parameter => "parameter",
        CandidateKind.Response => "response",
        CandidateKind.RequestBody => "requestBody",
        CandidateKind.Schema => "schema",
        CandidateKind.Header => "header",
        CandidateKind.Example => "example",
        CandidateKind.Enumeration => "enum",
        _ => "other",
    };
}
