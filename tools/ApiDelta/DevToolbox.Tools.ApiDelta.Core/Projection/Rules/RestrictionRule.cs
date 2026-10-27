using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>JSON Schema ne sait pas restreindre un schéma référencé.</summary>
/// <remarks>
/// L'inverse de l'extension, et bien plus coûteux : « allOf » ajoute, et rien n'enlève. Un type qui
/// restreint sa base en interdisant une valeur que la base admet n'a pas de forme JSON Schema, et en
/// produire une qui admettrait cette valeur contredirait le contrat.
/// </remarks>
public sealed class RestrictionRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public RestrictionRule()
        : base("derivation by restriction", ProjectionGapReason.Restriction)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { Derivation: Wsdl.XsdDerivation.Restriction, Content: not Wsdl.XsdContentKind.Simple };

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "restriction de « " + (subject.Type!.BaseType ?? "?") + " »";
}
