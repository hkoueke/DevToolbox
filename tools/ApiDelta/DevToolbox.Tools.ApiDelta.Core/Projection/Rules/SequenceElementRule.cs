using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un élément d'une séquence devient une propriété d'objet, portant son type.</summary>
/// <remarks>
/// La ligne la plus simple de la table, et celle dont tout le reste dépend. Les propriétés sortent dans
/// l'ordre de la séquence pour qu'un lecteur humain retrouve le contrat qu'il connaît ; que cet ordre ne
/// soit plus <em>exigé</em> est le sujet d'une autre ligne, et d'un écart.
/// <para>
/// « Portant son type » a été ajouté après coup, et c'est ce qui rend la ligne utile. Une première
/// version créait la propriété et s'arrêtait là : le document produit décrivait des objets dont chaque
/// propriété acceptait n'importe quoi, ce qu'une comparaison aurait trouvé parfaitement compatible avec
/// n'importe quelle production. Le défaut a été vu en vidant le document, pas en relisant le code.
/// </para>
/// </remarks>
public sealed class SequenceElementRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public SequenceElementRule()
        : base("Element in a `sequence`", "Object property", ProjectionConfidence.Carried)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) => subject?.Particle is not null;

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        into.Set("type", "object");

        SchemaDraft property = into.Property(subject.Particle!.Name);

        Shape(property, subject);
    }

    /// <summary>
    /// Donne sa forme à une propriété : un renvoi, ou un type intégré.
    /// </summary>
    /// <remarks>
    /// Pour une collection, la forme va dans « items » plutôt que sur la propriété : c'est le tableau qui
    /// est la propriété, et son contenu qui porte le type. L'ordre entre cette règle et celle des
    /// collections est donc indifférent, ce qui est voulu — la table ne promet aucun ordre entre deux
    /// lignes portées.
    /// </remarks>
    private static void Shape(SchemaDraft property, ProjectionSubject subject)
    {
        SchemaDraft target = subject.Particle!.IsCollection
            ? property.Section("items")
            : property;

        string type = subject.Particle.TypeName;

        if (subject.KnownTypes.Contains(type))
        {
            target.Set("$ref", "#/components/schemas/" + Local(type));

            return;
        }

        if (SimpleTypeFormatRule.Lookup(type) is { } mapped)
        {
            target.Set("type", mapped.Type);

            if (mapped.Format is { } format)
            {
                target.Set("format", format);
            }

            return;
        }

        // Ni un type du contrat, ni un type intégré reconnu. Cela ne peut arriver que si un import n'a
        // pas été résolu, ce que la lecture refuse déjà : le renvoi est posé quand même, pour que le
        // document reste cohérent plutôt que silencieusement permissif.
        target.Set("$ref", "#/components/schemas/" + Local(type));
    }

    private static string Local(string qualified)
    {
        int separator = qualified.LastIndexOf(':');

        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }
}
