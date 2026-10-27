using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un contenu mixte n'a pas de contrepartie.</summary>
/// <remarks>
/// Du texte et des éléments entremêlés, dont l'ordre porte le sens. Un objet JSON n'a pas de place où
/// mettre le texte entre deux propriétés, et une chaîne unique perdrait les éléments.
/// </remarks>
public sealed class MixedContentRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public MixedContentRule()
        : base("mixed content", ProjectionGapReason.MixedContent)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { Content: Wsdl.XsdContentKind.Mixed };

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "contenu mixte de « " + subject.Type!.Name + " »";
}
