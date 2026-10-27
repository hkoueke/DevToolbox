using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un en-tête SOAP se projette en paramètre d'en-tête, à peu près.</summary>
/// <remarks>
/// L'obligation survit — un en-tête requis reste un paramètre requis — mais le rôle SOAP ne survit pas.
/// Un en-tête destiné à un intermédiaire, qui doit le retirer avant de transmettre, devient un en-tête
/// HTTP ordinaire que le destinataire final verra. Ce n'est pas la même chose, et aucun paramètre
/// OpenAPI ne sait le dire.
/// </remarks>
public sealed class SoapHeaderRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public SoapHeaderRule()
        : base(
            "SOAP header",
            "a header parameter",
            ProjectionConfidence.Approximate,
            ProjectionGapReason.SoapRole)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Operation is { Input: { } input } && input.Parts.Count > 1;

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        // Au-delà de la première, chaque partie du message d'entrée est un en-tête : le corps est une
        // partie unique en « document/literal ».
        foreach (Wsdl.WsdlPart part in subject.Operation!.Input!.Parts.Skip(1))
        {
            SchemaDraft parameter = new();

            parameter.Set("name", part.Name);
            parameter.Set("in", "header");
            parameter.Set("required", true);

            into.Append("parameters", parameter);
        }

        Record(
            subject,
            (subject.Operation.Input.Parts.Count - 1) + " en-tête(s) de « "
                + subject.Operation.Name + " »",
            gaps);
    }
}
