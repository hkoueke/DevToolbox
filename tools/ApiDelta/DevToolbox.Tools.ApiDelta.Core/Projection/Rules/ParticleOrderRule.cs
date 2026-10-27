using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>L'ordre des particules dans une séquence : porté dans la forme, pas dans le contrat.</summary>
/// <remarks>
/// La distinction est fine et elle compte. Les propriétés sortent dans l'ordre de la séquence, si bien
/// qu'un lecteur humain retrouve l'ordre ; mais un objet JSON a des membres non ordonnés, donc le
/// contrat produit n'<em>exige</em> plus cet ordre. Un service qui le vérifie refuserait un message que
/// le contrat projeté déclare valide.
/// </remarks>
public sealed class ParticleOrderRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public ParticleOrderRule()
        : base("particle order inside a `sequence`", ProjectionGapReason.ParticleOrder)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { Content: Wsdl.XsdContentKind.Sequence, Particles.Count: > 1 };

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "ordre de " + subject.Type!.Particles.Count + " particules dans « " + subject.Type.Name + " »";
}
