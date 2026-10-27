using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>« totalDigits » et « fractionDigits » n'ont aucun équivalent en JSON Schema.</summary>
/// <remarks>
/// Un « multipleOf » de 0,01 approcherait « fractionDigits="2" » sans le dire : il contraint la valeur,
/// là où la facette contraint l'écriture. Deux choses différentes, et la deuxième est précisément celle
/// qu'un service financier a mise dans son contrat.
/// </remarks>
public sealed class NumericPrecisionRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public NumericPrecisionRule()
        : base("`totalDigits`, `fractionDigits`", ProjectionGapReason.NumericPrecision)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Facet is { Kind: "totalDigits" or "fractionDigits" };

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "facette « " + subject.Facet!.Kind + " » valant " + subject.Facet.Value;
}
