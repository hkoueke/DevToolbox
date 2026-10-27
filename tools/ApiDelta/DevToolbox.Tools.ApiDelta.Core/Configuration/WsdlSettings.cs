namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Ce qu'une équipe décide de la lecture et de la projection d'un contrat de service.</summary>
/// <param name="Limits">Le budget de lecture.</param>
/// <param name="Imports">Ce que la résolution des imports s'autorise.</param>
/// <param name="Verbs">La table de verbes de la dérivation d'itinéraires.</param>
/// <remarks>
/// Une section à part plutôt que des membres dispersés dans le jeu de règles : les trois répondent à la
/// même question — jusqu'où l'outil va-t-il, et avec quelles conventions — et une équipe qui adopte un
/// contrat de service les règle ensemble ou pas du tout.
/// <para>
/// Les valeurs livrées forment une configuration utilisable. Une équipe adopte un jeu de règles parce
/// qu'elle est en désaccord avec l'une d'elles, non pour commencer à travailler.
/// </para>
/// </remarks>
public sealed record WsdlSettings(
    WsdlLimits? Limits = null,
    ImportPolicy? Imports = null,
    IReadOnlyDictionary<string, string>? Verbs = null)
{
    /// <summary>Les réglages livrés.</summary>
    public static WsdlSettings Default { get; } = new();

    /// <summary>Le budget de lecture, jamais nul.</summary>
    public WsdlLimits Limits { get; init; } = Limits ?? WsdlLimits.Default;

    /// <summary>
    /// La politique d'imports, jamais nulle.
    /// </summary>
    /// <remarks>
    /// Sa racine de confinement est vide tant que personne ne l'a écrite : elle n'est pas une décision
    /// d'équipe mais une donnée de l'exécution — le dossier du contrat qu'on lit. L'équipe peut en
    /// nommer une, et c'est alors la sienne qui vaut.
    /// </remarks>
    public ImportPolicy Imports { get; init; } = Imports ?? new ImportPolicy(string.Empty);

    /// <summary>
    /// La table de verbes, vide tant que personne n'en a écrit une.
    /// </summary>
    /// <remarks>
    /// Vide, et non recopiée depuis celle qui est livrée. La distinction compte au moment de lire une
    /// configuration : une table écrite <em>remplace</em> celle de l'outil, et une équipe qui n'en écrit
    /// que trois verbes veut ses trois verbes, non les siens ajoutés aux trente-six d'origine.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Verbs { get; init; } =
        Verbs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>La politique d'imports à appliquer pour un contrat donné.</summary>
    /// <param name="folder">Le dossier du contrat, employé si l'équipe n'a pas nommé de racine.</param>
    /// <returns>La politique, racine comprise.</returns>
    public ImportPolicy ConfinedTo(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        return Imports.ConfinementRoot.Length > 0 ? Imports : Imports with { ConfinementRoot = folder };
    }

    /// <summary>La table de verbes à employer, ou rien pour celle qui est livrée.</summary>
    public IReadOnlyDictionary<string, string>? Table => Verbs.Count > 0 ? Verbs : null;
}
