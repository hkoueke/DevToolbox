using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un « xsd:choice » se projette en « oneOf », à peu près.</summary>
/// <remarks>
/// XSD choisit par particule : exactement une branche apparaît. « oneOf » choisit par validation :
/// exactement un sous-schéma doit valider. Quand deux branches se recouvrent — deux objets dont les
/// propriétés obligatoires se croisent — un document valide pour les deux satisfait XSD et échoue à
/// « oneOf ». La forme est la bonne, la sémantique diffère sur les bords, et c'est pourquoi le constat
/// porte une confiance incertaine au lieu d'une équivalence.
/// <para>
/// Chaque branche reçoit les règles de particule de la table, et non une forme écrite ici. Une première
/// version posait un « $comment » portant le nom du type : le mot-clé n'existe pas en OpenAPI 3.0, si
/// bien que le document produit était refusé par sa propre revalidation dès qu'il était inséré en
/// production — et la branche ne disait rien du type qu'elle attendait. Le défaut n'était visible qu'au
/// bout de la chaîne, là où le document doit être <em>valide</em>, et non seulement relisible.
/// </para>
/// </remarks>
public sealed class ChoiceRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public ChoiceRule()
        : base(
            "`xsd:choice`",
            "`oneOf`",
            ProjectionConfidence.Approximate,
            ProjectionGapReason.ExclusiveByParticle)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { Content: Wsdl.XsdContentKind.Choice };

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        foreach (Wsdl.XsdParticle particle in subject.Type!.Particles)
        {
            SchemaDraft branch = new();

            // Les règles de particule, appliquées dans la branche : la propriété y reçoit son type, sa
            // forme de collection et son obligation exactement comme ailleurs. C'est aussi pourquoi le
            // projecteur ne les applique pas, lui, aux particules d'un choix — les appliquer au niveau
            // du type déclarerait les trois branches obligatoires à la fois, soit l'inverse d'un choix.
            ProjectionSubject inBranch = subject with
            {
                Type = null,
                Particle = particle,
                Location = particle.Location,
            };

            foreach (IProjectionRule rule in ProjectionTable.For(inBranch))
            {
                rule.Apply(inBranch, branch, gaps);
            }

            into.Append("oneOf", branch);
        }

        Record(subject, "choix de " + subject.Type.Particles.Count + " branches", gaps);
    }
}
