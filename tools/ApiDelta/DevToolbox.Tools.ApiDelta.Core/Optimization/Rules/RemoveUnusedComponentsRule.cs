using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-007 : retire les composants qu'aucun chemin n'atteint.
/// </summary>
/// <remarks>
/// La sûreté de cette règle ne dépend pas d'elle mais de ce que l'analyse des références a pu établir.
/// Une référence non résolue ou préservée hors du document laisse ouverte la possibilité qu'un composant
/// soit atteint d'ailleurs, et la transformation passe alors en relecture — c'est AUD-003 qui le
/// détermine, et la classe de sûreté voyage avec la proposition.
/// <para>
/// La règle ne retire que ce que le plan nomme. Elle ne recalcule pas l'accessibilité : si elle le
/// faisait, elle pourrait retirer plus que ce qui a été relu et approuvé.
/// </para>
/// </remarks>
public sealed class RemoveUnusedComponentsRule : IOptimizationRule
{
    /// <inheritdoc />
    public string RuleId => "OPT-007";

    /// <inheritdoc />
    public bool EnabledByDefault => OptimizationDefaults.EnabledByDefault(RuleId);

    /// <inheritdoc />
    public bool Removes => true;

    /// <inheritdoc />
    public Result<SpecNode> Apply(SpecNode root, Transformation transformation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(transformation);

        SpecNode edited = root;

        foreach (SpecPointer origin in transformation.Origins)
        {
            if (PointerNavigator.Find(edited, origin.Json) is null)
            {
                return Result.Fail<SpecNode>(
                    FailureReason.InvalidInput,
                    "Le composant « " + origin.Json + " » n'est plus là : le retirer deux fois "
                    + "retirerait quelque chose d'autre.");
            }

            edited = TreeEdits.Remove(edited, origin);
        }

        return Result.Success(edited);
    }
}
