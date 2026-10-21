using DevToolbox.Tools.ApiDelta.Core.Metamodel;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les corps de requête dont la description ne dit pas s'ils sont obligatoires.
/// </summary>
/// <remarks>
/// OpenAPI tranche pour l'auteur : à défaut, le corps est facultatif. Un service qui refuse une requête
/// sans corps décrit donc l'inverse de ce qu'il fait, et le client engendré rend le paramètre facultatif.
/// <para>
/// Le remède passe en relecture, et non en sûr : ajouter <c>required: true</c> change ce que la
/// description promet. Le constat dit qu'il manque une décision, pas laquelle.
/// </para>
/// </remarks>
public sealed class RequestBodyRequirednessRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public RequestBodyRequirednessRule()
        : base("AUD-007", SpecObjectKind.RequestBody)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. subject.Nodes(SpecObjectKind.RequestBody)
                .Where(body => !Nodes.Has(body.Node, "required"))
                .Where(body => !Nodes.Has(body.Node, "$ref"))
                .Select(body => Report(
                    "Le corps de requête ne dit pas s'il est obligatoire.",
                    body.Location,
                    "Déclarer « required » explicitement : à défaut, OpenAPI le tient pour facultatif.")),
        ]);
    }
}
