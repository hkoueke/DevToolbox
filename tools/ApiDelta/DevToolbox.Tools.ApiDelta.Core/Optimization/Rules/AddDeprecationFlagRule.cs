using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-009 : pose <c>deprecated: true</c> là où la description annonce déjà une dépréciation.
/// Désactivée par défaut.
/// </summary>
/// <remarks>
/// Désactivée parce qu'elle change ce que la description publie. La phrase est pour un lecteur humain ;
/// le drapeau est pour tout le reste — le portail, le client engendré, la passerelle qui compte les
/// appels à ce qui devrait disparaître. Le poser est presque toujours la bonne chose à faire, et fera
/// apparaître un avertissement de compilation chez chaque appelant : souhaitable, mais pas en silence.
/// <para>
/// La règle refuse de poser le drapeau là où il est déjà, plutôt que de l'écraser. Un <c>deprecated:
/// false</c> explicite est une décision, et la contredire sans le dire serait pire que de ne rien faire.
/// </para>
/// </remarks>
public sealed class AddDeprecationFlagRule : IOptimizationRule
{
    private const string Field = "deprecated";

    /// <inheritdoc />
    public string RuleId => "OPT-009";

    /// <inheritdoc />
    public bool EnabledByDefault => OptimizationDefaults.EnabledByDefault(RuleId);

    /// <inheritdoc />
    public bool Removes => false;

    /// <inheritdoc />
    public Result<SpecNode> Apply(SpecNode root, Transformation transformation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(transformation);

        SpecNode edited = root;
        int posed = 0;

        foreach (SpecPointer origin in transformation.Origins)
        {
            if (PointerNavigator.Find(edited, origin.Json) is not MappingNode node
                || node.Find(Field) is not null)
            {
                continue;
            }

            edited = TreeEdits.Insert(edited, origin.Child(Field), TreeEdits.Flag(true));
            posed++;
        }

        return posed == 0
            ? Result.Fail<SpecNode>(
                FailureReason.InvalidInput,
                "Le drapeau de dépréciation est déjà posé partout où cette proposition le visait.")
            : Result.Success(edited);
    }
}
