using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Une règle d'audit.</summary>
/// <remarks>
/// Une règle déclare les sortes d'objets qu'elle regarde et rend un rapport. Elle ne parcourt pas l'arbre
/// et ne garde rien : c'est le moteur qui parcourt une fois et distribue, et c'est ce qui permet à
/// quarante-sept règles de coûter une traversée au lieu de quarante-sept.
/// </remarks>
public interface IAuditRule
{
    /// <summary>L'identifiant de la règle, qui fait partie de la sortie et ne change pas.</summary>
    string RuleId { get; }

    /// <summary>Les sortes d'objets que la règle regarde.</summary>
    /// <remarks>
    /// Déclaratif, parce que le moteur s'en sert pour ne réveiller une règle que sur ce qui la concerne.
    /// Une règle qui regarde le document entier déclare <see cref="SpecObjectKind.Document"/>.
    /// </remarks>
    IReadOnlyList<SpecObjectKind> Visits { get; }

    /// <summary>Évalue la règle.</summary>
    /// <param name="subject">Le document, déjà parcouru.</param>
    /// <returns>Ses constats, et l'état dans lequel elle les rend.</returns>
    RuleReport Evaluate(AuditSubject subject);
}
