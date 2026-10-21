using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// Ce que toutes les extractions font : poser une écriture dans <c>components</c>, et remplacer chaque
/// occurrence par une référence à elle.
/// </summary>
/// <remarks>
/// L'édition est la même pour un paramètre, une réponse, un corps de requête, un en-tête, un exemple ou
/// un schéma, et elle est écrite une fois. Les règles restent distinctes parce qu'une équipe les active
/// séparément et parce que leur sûreté ne se démontre pas de la même façon — pas parce que leur mécanique
/// diffère.
/// <para>
/// L'ordre des deux éditions compte. Le composant est posé d'abord, depuis l'arbre d'origine, puis les
/// occurrences sont remplacées. Poser le composant après aurait recopié une occurrence déjà remplacée par
/// une référence, c'est-à-dire un composant qui se référence lui-même.
/// </para>
/// </remarks>
public abstract class ExtractionRule : IOptimizationRule
{
    /// <summary>Crée la règle.</summary>
    /// <param name="ruleId">Son identifiant.</param>
    protected ExtractionRule(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        RuleId = ruleId;
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <inheritdoc />
    public bool EnabledByDefault => OptimizationDefaults.EnabledByDefault(RuleId);

    /// <inheritdoc />
    public bool Removes => false;

    /// <inheritdoc />
    public Result<SpecNode> Apply(SpecNode root, Transformation transformation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(transformation);

        if (transformation.Destination is not { } destination)
        {
            return Result.Fail<SpecNode>(
                FailureReason.InvalidInput,
                "Cette extraction n'a pas de destination : il n'y a rien où poser le composant.");
        }

        if (transformation.Origins.Count == 0
            || PointerNavigator.Find(root, transformation.Origins[0].Json) is not { } shared)
        {
            return Result.Fail<SpecNode>(
                FailureReason.InvalidInput,
                "L'occurrence à extraire ne se trouve plus dans la description : "
                + "une transformation précédente l'a déjà déplacée.");
        }

        // Aucune origine ne doit contenir la destination. Remplacer une origine qui engloberait le
        // composant qu'on vient d'y poser emporterait ce composant avec elle — et dans le cas limite où
        // l'origine est la racine, le document entier.
        if (transformation.Origins.Any(origin => origin.Contains(destination)))
        {
            return Result.Fail<SpecNode>(
                FailureReason.InvalidInput,
                "L'origine engloberait sa propre destination : extraire ici emporterait le composant "
                + "que l'extraction vient de poser.");
        }

        // La destination doit être libre. Deux groupes qui se suggèrent le même nom, ou un nom qui
        // existe déjà, feraient que la seconde écriture écrase la première — et les références de la
        // première désigneraient alors le contenu de la seconde. L'une des deux doit céder, et ce n'est
        // pas à une édition de choisir en silence.
        if (PointerNavigator.Find(root, destination.Json) is not null)
        {
            return Result.Fail<SpecNode>(
                FailureReason.InvalidInput,
                "« " + destination.Json + " » existe déjà : extraire ici écraserait ce qui y est, et "
                + "les références qui le désignent pointeraient vers autre chose.");
        }

        SpecNode edited = TreeEdits.Insert(root, destination, shared);
        MappingNode reference = TreeEdits.Reference("#" + destination.Json);

        foreach (SpecPointer origin in transformation.Origins)
        {
            edited = TreeEdits.Replace(edited, origin, reference);
        }

        return Result.Success(edited);
    }
}
