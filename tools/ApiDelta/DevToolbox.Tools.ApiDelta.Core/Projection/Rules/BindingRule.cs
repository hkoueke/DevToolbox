using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Le style, l'usage et le transport d'une liaison n'ont pas de contrepartie.</summary>
/// <remarks>
/// Ce sont des faits sur l'enveloppe, pas sur les données : une liaison « rpc/encoded » décrit un corps
/// dont la forme ne se déduit pas du schéma, et c'est pour cela que la lecture le signale déjà. Les
/// taire laisserait croire que la projection a vu un contrat dont elle n'a vu que la moitié.
/// </remarks>
public sealed class BindingRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public BindingRule()
        : base("binding `style`, `use`, transport", ProjectionGapReason.Binding)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) => subject?.Binding is not null;

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "liaison « " + subject.Binding!.Style + "/" + subject.Binding.Use + " »";
}
