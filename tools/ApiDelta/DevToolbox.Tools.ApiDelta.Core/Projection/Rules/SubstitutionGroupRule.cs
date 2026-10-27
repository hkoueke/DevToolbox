using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un groupe de substitution, ou une tête abstraite, n'a pas de contrepartie.</summary>
/// <remarks>
/// Un « oneOf » des membres approcherait la chose, mais perdrait ce qui fait un groupe de substitution :
/// la tête est le point d'extension, et un membre ajouté demain y entre sans que le contrat change. Un
/// « oneOf » fermerait cette porte en prétendant décrire le contrat tel qu'il est.
/// </remarks>
public sealed class SubstitutionGroupRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public SubstitutionGroupRule()
        : base("substitution group, abstract head", ProjectionGapReason.SubstitutionGroup)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { } type && (type.SubstitutionMembers.Count > 0 || type.Abstract);

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        subject.Type!.SubstitutionMembers.Count > 0
            ? "groupe de substitution de " + subject.Type.SubstitutionMembers.Count + " membre(s)"
            : "tête abstraite";
}
