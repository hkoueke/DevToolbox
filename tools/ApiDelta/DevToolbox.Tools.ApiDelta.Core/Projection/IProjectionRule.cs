namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Une ligne de la table de projection publiée, rendue exécutable.</summary>
/// <remarks>
/// Une règle par ligne, et la ligne portée mot pour mot. Un essai lit le fichier publié et vérifie que le
/// jeu de règles le couvre <em>exactement</em> : ni une ligne sans règle, ni une règle sans ligne. C'est
/// la forme que 003 emploie déjà pour son catalogue de règles, et elle existe parce qu'une table de
/// correspondance qui cesse discrètement de décrire le code est pire qu'une absence de table — quelqu'un
/// s'y fiera.
/// </remarks>
public interface IProjectionRule
{
    /// <summary>La première colonne de la ligne, mot pour mot.</summary>
    string Source { get; }

    /// <summary>La deuxième colonne de la ligne, mot pour mot.</summary>
    string Target { get; }

    /// <summary>Ce que cette projection vaut.</summary>
    ProjectionConfidence Confidence { get; }

    /// <summary>Le motif d'écart, pour une projection approchante ou absente.</summary>
    ProjectionGapReason? Reason { get; }

    /// <summary>Indique que cette règle a quelque chose à dire sur ce sujet.</summary>
    /// <param name="subject">Le sujet.</param>
    /// <returns>Vrai si la règle s'applique.</returns>
    bool Matches(ProjectionSubject subject);

    /// <summary>Applique la règle.</summary>
    /// <param name="subject">Le sujet.</param>
    /// <param name="into">Le brouillon à compléter.</param>
    /// <param name="gaps">Où consigner un écart.</param>
    void Apply(ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps);
}
