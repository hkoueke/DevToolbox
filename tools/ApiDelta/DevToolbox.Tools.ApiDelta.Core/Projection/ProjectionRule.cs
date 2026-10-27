namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>La part commune d'une règle : la ligne qu'elle rend exécutable.</summary>
/// <remarks>
/// Les quatre premières propriétés sont la ligne publiée, recopiée mot pour mot. Les placer ici plutôt
/// que de les laisser à chaque règle n'est pas une économie de frappe : c'est ce qui permet à l'essai de
/// comparer le jeu de règles à la table sans que chaque règle ait sa façon de se décrire.
/// </remarks>
public abstract class ProjectionRule : IProjectionRule
{
    /// <summary>Crée la règle.</summary>
    /// <param name="source">La première colonne.</param>
    /// <param name="target">La deuxième colonne.</param>
    /// <param name="confidence">Ce que la projection vaut.</param>
    /// <param name="reason">Le motif d'écart, le cas échéant.</param>
    protected ProjectionRule(
        string source,
        string target,
        ProjectionConfidence confidence,
        ProjectionGapReason? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        Source = source;
        Target = target;
        Confidence = confidence;
        Reason = reason;
    }

    /// <inheritdoc />
    public string Source { get; }

    /// <inheritdoc />
    public string Target { get; }

    /// <inheritdoc />
    public ProjectionConfidence Confidence { get; }

    /// <inheritdoc />
    public ProjectionGapReason? Reason { get; }

    /// <inheritdoc />
    public abstract bool Matches(ProjectionSubject subject);

    /// <inheritdoc />
    public abstract void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps);

    /// <summary>Consigne un écart pour ce sujet.</summary>
    /// <param name="subject">Le sujet.</param>
    /// <param name="construct">Ce dont il s'agit, tel que XSD l'écrit.</param>
    /// <param name="gaps">Où le consigner.</param>
    protected void Record(
        ProjectionSubject subject, string construct, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(gaps);

        gaps.Add(new ProjectionGap(
            Reason!.Value, Confidence, construct, subject.Owner, subject.Location));
    }
}
