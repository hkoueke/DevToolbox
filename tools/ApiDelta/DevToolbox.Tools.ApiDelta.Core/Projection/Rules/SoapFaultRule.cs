using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un défaut SOAP se projette en code de statut et corps de réponse, à peu près.</summary>
/// <remarks>
/// Le code de statut est <em>déduit</em>, non déclaré : un WSDL ne dit nulle part qu'un défaut vaut 400
/// plutôt que 500. L'outil choisit 500 parce qu'un défaut SOAP non qualifié est une erreur de
/// traitement, et il le dit dans le rapport — un code annoncé comme venant du contrat alors qu'il vient
/// d'une convention est exactement ce que cette fonctionnalité promet de ne pas faire.
/// </remarks>
public sealed class SoapFaultRule : ProjectionRule
{
    /// <summary>Le statut retenu, faute que le contrat en déclare un.</summary>
    public const string DerivedStatus = "500";

    /// <summary>Crée la règle.</summary>
    public SoapFaultRule()
        : base(
            "SOAP fault",
            "status code and response body",
            ProjectionConfidence.Approximate,
            ProjectionGapReason.DerivedStatusCode)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Operation is { } operation && operation.Faults.Count > 0;

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        IReadOnlyList<Wsdl.WsdlMessage> faults = subject.Operation!.Faults;

        // Une seule entrée, et non une par défaut : tous les défauts d'une opération aboutissent au même
        // statut déduit, et en produire plusieurs sous la même clé reviendrait à n'en garder qu'un au
        // hasard de l'ordre d'écriture.
        SchemaDraft response = new();

        response.Set(
            "description",
            "Défaut SOAP : " + string.Join(", ", faults.Select(fault => fault.Name))
                + ". Statut déduit, non déclaré par le contrat.");

        into.Section("responses").Set(DerivedStatus, response);

        Record(subject, faults.Count + " défaut(s) de « " + subject.Operation.Name + " »", gaps);
    }
}
