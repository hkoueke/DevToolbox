namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Ce qu'une équipe a décidé, et ce que l'outil décide à défaut.</summary>
/// <remarks>
/// Les valeurs par défaut forment une configuration utilisable : une équipe adopte un jeu de règles quand
/// elle est en désaccord avec l'une d'elles, non pour commencer à travailler. Les valeurs propres à une
/// organisation — le préfixe d'extension protégé, la phrase de dépréciation, la forme des substituants —
/// sont présentes mais remplaçables, si bien que l'outil sert sur le corpus dont il est issu sans y être
/// attaché.
/// </remarks>
public sealed record Ruleset
{
    /// <summary>Le jeu de règles par défaut.</summary>
    public static Ruleset Default { get; } = new();

    /// <summary>Ce qui est décidé règle par règle, par identifiant.</summary>
    public IReadOnlyDictionary<string, RuleSetting> Rules { get; init; } =
        new Dictionary<string, RuleSetting>(StringComparer.Ordinal);

    /// <summary>
    /// Les préfixes d'extension protégés. Cette liste ne peut qu'ajouter de la protection : aucun jeu de
    /// règles ne rend une extension inconnue éligible à une suppression automatique.
    /// </summary>
    public IReadOnlyList<string> ProtectedExtensionPrefixes { get; init; } = ["x-sg-"];

    /// <summary>
    /// Les composants à conserver même inaccessibles, par nom ou par préfixe suivi d'une étoile.
    /// </summary>
    /// <remarks>
    /// Un composant peut n'être atteint que de l'extérieur du document, et l'outil ne le saurait pas. Un
    /// motif de conservation est la façon de le lui dire, et un composant qu'il apparie n'est jamais
    /// proposé à la suppression.
    /// </remarks>
    public IReadOnlyList<string> KeepComponentPatterns { get; init; } = [];

    /// <summary>Les phrases qui annoncent une dépréciation dans une description.</summary>
    public IReadOnlyList<string> DeprecationPhrases { get; init; } =
        ["Endpoint obsolète", "Deprecated endpoint"];

    /// <summary>Les en-têtes que les opérations comparables sont censées porter.</summary>
    public IReadOnlyList<string> StandardHeaders { get; init; } = ["X-serviceEmetteur", "X-correlationId"];

    /// <summary>Les réponses d'erreur que les opérations comparables sont censées déclarer.</summary>
    public IReadOnlyList<string> StandardResponses { get; init; } = ["400", "401", "403", "404", "500"];

    /// <summary>La forme des substituants d'environnement attendus dans les serveurs.</summary>
    public string EnvironmentPlaceholderPattern { get; init; } = @"\$\((?<name>[A-Za-z0-9_.-]+)\)";

    /// <summary>Les substituants connus. Un substituant inconnu est signalé.</summary>
    public IReadOnlyList<string> KnownPlaceholders { get; init; } = ["catalog.host", "catalog.name"];

    /// <summary>Les conventions de nommage, s'il y en a.</summary>
    public NamingPolicy Naming { get; init; } = NamingPolicy.Default;

    /// <summary>
    /// Les extensions de fournisseur dont l'outil connaît la forme, et les champs qu'elles doivent porter.
    /// </summary>
    /// <remarks>
    /// Vide par défaut : une extension dont la forme n'est pas déclarée reste opaque, et l'outil n'a rien à
    /// dire de sa structure. Ce silence est volontaire — supposer la forme d'une extension inconnue serait
    /// la façon la plus directe d'en casser une.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> KnownExtensionFields { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>La mise en forme canonique.</summary>
    public CanonicalFormatting Formatting { get; init; } = CanonicalFormatting.Default;

    /// <summary>La politique de référence.</summary>
    public ReferencePolicy References { get; init; } = ReferencePolicy.Closed;

    /// <summary>Les limites de lecture.</summary>
    public LoadLimits Limits { get; init; } = LoadLimits.Default;

    /// <summary>La politique de versionnage.</summary>
    public VersioningPolicy Versioning { get; init; } = VersioningPolicy.Default;

    /// <summary>Les réglages de l'extraction.</summary>
    public ExtractSettings Extract { get; init; } = ExtractSettings.Default;

    /// <summary>Le seuil d'échec.</summary>
    public FailureThreshold Threshold { get; init; } = FailureThreshold.Never;

    /// <summary>Exige qu'une mise en sourdine porte une justification.</summary>
    public bool RequireSuppressionJustification { get; init; } = true;

    /// <summary>Le nombre maximal de constats rapportés par règle, avant troncature annoncée.</summary>
    public int MaxFindingsPerRule { get; init; } = 500;

    /// <summary>La taille maximale d'un groupe de quasi-doublons comparés entre eux.</summary>
    public int MaxNearDuplicateBucket { get; init; } = 200;

    /// <summary>La croissance tolérée en sortie, en pourcentage. Zéro refuse toute croissance.</summary>
    public int MaxGrowthPercent { get; init; }

    /// <summary>Le réglage en vigueur pour une règle.</summary>
    /// <param name="ruleId">L'identifiant de la règle.</param>
    /// <returns>Ce que l'équipe a décidé, ou le réglage par défaut.</returns>
    public RuleSetting SettingFor(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        return Rules.TryGetValue(ruleId, out RuleSetting? setting) ? setting : RuleSetting.Default;
    }
}
