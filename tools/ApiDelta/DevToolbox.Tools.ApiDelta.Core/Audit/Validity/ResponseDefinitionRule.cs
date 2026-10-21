using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte ce qui manque ou se contredit dans les réponses d'une opération.
/// </summary>
/// <remarks>
/// Trois défauts constatables : pas de réponse du tout, une réponse sans description, et une clé qui n'est
/// pas un code de statut.
/// <para>
/// Deux clés qui désignent le même code — <c>'400'</c> et <c>400</c> dans la même opération — ne sont pas
/// examinées ici, et ce n'est pas un oubli : la lecture résout les scalaires avant de chercher les clés en
/// double, si bien qu'une telle description est refusée par le lecteur et n'atteint jamais l'examen. Une
/// règle qui la guetterait serait un garde-fou qui ne garde rien.
/// </para>
/// <para>
/// Un dernier constat est une déduction, non un fait : qu'une opération ne déclare pas une réponse
/// d'erreur que ses voisines déclarent. Il part donc avec son raisonnement.
/// </para>
/// </remarks>
public sealed class ResponseDefinitionRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public ResponseDefinitionRule()
        : base("AUD-006", SpecObjectKind.Operation)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<AuditNode> operations = subject.Nodes(SpecObjectKind.Operation);
        List<Finding> findings = [];

        foreach (AuditNode operation in operations)
        {
            if (Nodes.Map(operation.Node, "responses") is not { } responses)
            {
                findings.Add(Report(
                    "L'opération ne déclare aucune réponse.",
                    operation.Location,
                    "Déclarer au moins la réponse du cas nominal."));

                continue;
            }

            findings.AddRange(Undescribed(subject.Root, responses));
            findings.AddRange(Malformed(responses));
        }

        findings.AddRange(Inconsistent(subject, operations));

        return RuleReport.Evaluated(findings);
    }

    /// <summary>Les réponses sans description, que la spécification d'OpenAPI exige.</summary>
    private IEnumerable<Finding> Undescribed(SpecNode root, MappingNode responses) =>
        responses.Entries
            .Where(entry => Nodes.Resolve(root, entry.Value) is { } resolved
                && string.IsNullOrWhiteSpace(Nodes.Text(resolved, "description")))
            .Select(entry => Report(
                $"La réponse « {entry.Key.Text} » n'a pas de description.",
                entry.Value.Location,
                "Décrire ce que la réponse signifie pour l'appelant."));

    /// <summary>Les clés qui ne sont ni un code de statut ni « default ».</summary>
    private IEnumerable<Finding> Malformed(MappingNode responses) =>
        responses.Entries
            .Where(entry => !IsStatusKey(entry.Key.Text))
            .Select(entry => Report(
                $"« {entry.Key.Text} » n'est pas un code de statut.",
                entry.Value.Location,
                "Employer un code à trois chiffres, une plage telle que « 4XX », ou « default »."));

    /// <summary>
    /// Les réponses d'erreur qu'une opération ne déclare pas alors que toutes ses voisines les déclarent.
    /// </summary>
    /// <remarks>
    /// Les réponses que le jeu de règles déclare standard sont écartées : leur absence relève de la règle
    /// de gouvernance, et deux règles qui disent la même chose font un rapport deux fois plus long sans
    /// rien apprendre. Ce qui reste ici est l'usage que le document s'est donné à lui-même, sans qu'aucun
    /// jeu de règles l'ait demandé.
    /// </remarks>
    private IEnumerable<Finding> Inconsistent(AuditSubject subject, IReadOnlyList<AuditNode> operations)
    {
        HashSet<string> declared = new(
            operations
                .SelectMany(operation => Codes(operation))
                .Where(code => code.StartsWith('4') || code.StartsWith('5'))
                .Where(code => !subject.Ruleset.StandardResponses.Contains(code, StringComparer.Ordinal)),
            StringComparer.Ordinal);

        foreach (AuditNode operation in operations)
        {
            HashSet<string> own = new(Codes(operation), StringComparer.Ordinal);

            IReadOnlyList<string> absent =
            [
                .. declared.Where(code => !own.Contains(code))
                    .OrderBy(code => code, StringComparer.Ordinal),
            ];

            if (absent.Count == 0 || own.Count == 0)
            {
                continue;
            }

            yield return Report(
                "L'opération ne déclare pas les réponses d'erreur que ses voisines déclarent : "
                    + string.Join(", ", absent),
                operation.Location,
                "Déclarer ces réponses, ou dire pourquoi cette opération en est dispensée.") with
            {
                Evidence = new FindingEvidence(
                    Reasoning: "Déduit de ce que les autres opérations de la description déclarent, "
                        + "non d'une règle que la description énonce."),
            };
        }
    }

    private static IEnumerable<string> Codes(AuditNode operation) =>
        Nodes.Map(operation.Node, "responses") is { } responses
            ? responses.Entries.Select(entry => Semantic(entry.Key))
            : [];

    /// <summary>Le code que désigne une clé, quelle que soit son écriture.</summary>
    private static string Semantic(ScalarNode key) => key.Resolved.Canonical;

    private static bool IsStatusKey(string key)
    {
        if (string.Equals(key, "default", StringComparison.Ordinal))
        {
            return true;
        }

        if (key.Length != 3 || key[0] is < '1' or > '5')
        {
            return false;
        }

        return (key[1] == 'X' && key[2] == 'X')
            || (char.IsAsciiDigit(key[1]) && char.IsAsciiDigit(key[2]));
    }
}
