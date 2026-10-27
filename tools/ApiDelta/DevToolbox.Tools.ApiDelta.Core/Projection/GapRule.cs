namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Une règle qui ne porte rien, et le dit.</summary>
/// <remarks>
/// Les neuf lignes sans contrepartie font toutes la même chose : elles consignent un écart et ne touchent
/// pas au brouillon. Le point qui mérite d'être écrit une seule fois est le second — une règle non portée
/// qui déposerait tout de même quelque chose dans le schéma produirait une description affirmant ce que la
/// projection vient de déclarer impossible.
/// </remarks>
public abstract class GapRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    /// <param name="source">La première colonne.</param>
    /// <param name="reason">Le motif.</param>
    protected GapRule(string source, ProjectionGapReason reason)
        : base(source, "—", ProjectionConfidence.NotCarried, reason)
    {
    }

    /// <summary>Comment nommer la construction dans l'écart.</summary>
    /// <param name="subject">Le sujet.</param>
    /// <returns>Le nom de la construction.</returns>
    protected abstract string Construct(ProjectionSubject subject);

    /// <inheritdoc />
    public sealed override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);

        Record(subject, Construct(subject), gaps);
    }
}
