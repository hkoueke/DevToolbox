using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce qu'une règle reçoit : le document, déjà parcouru, et rien de mutable.</summary>
/// <param name="Document">La description auditée.</param>
/// <param name="Ruleset">Ce que l'équipe a décidé.</param>
/// <param name="NodesByKind">Les nœuds du document, rangés par sorte d'objet.</param>
/// <param name="Fingerprinter">Donne l'empreinte d'un sous-arbre.</param>
/// <param name="References">Le graphe des références, quand il a pu être construit.</param>
/// <param name="StructuralVerdicts">
/// Ce que la validation structurelle a trouvé à dire, ou rien si elle n'a pas eu lieu.
/// </param>
/// <remarks>
/// Une règle ne parcourt pas l'arbre et ne retient rien d'une exécution à l'autre : elle reçoit les nœuds
/// de la sorte qui l'intéresse et rend son rapport. C'est ce qui permet à la passe d'être unique et aux
/// règles d'être sans état, donc réutilisables et vérifiables une par une.
/// </remarks>
public sealed record AuditSubject(
    SpecDocument Document,
    Ruleset Ruleset,
    IReadOnlyDictionary<SpecObjectKind, IReadOnlyList<AuditNode>> NodesByKind,
    IFingerprinter Fingerprinter,
    ReferenceGraphResult? References,
    IReadOnlyList<Diagnostic>? StructuralVerdicts)
{
    /// <summary>La version déclarée par le document.</summary>
    public OpenApiVersion Version => Document.DeclaredVersion;

    /// <summary>La racine de l'arbre.</summary>
    public SpecNode Root => Document.Root;

    /// <summary>Les nœuds d'une sorte donnée, dans l'ordre de la traversée.</summary>
    /// <param name="kind">La sorte cherchée.</param>
    /// <returns>Les nœuds, ou une liste vide.</returns>
    public IReadOnlyList<AuditNode> Nodes(SpecObjectKind kind) =>
        NodesByKind.TryGetValue(kind, out IReadOnlyList<AuditNode>? nodes) ? nodes : [];
}
