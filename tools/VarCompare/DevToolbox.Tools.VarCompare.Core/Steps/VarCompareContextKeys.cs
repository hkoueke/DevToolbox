namespace DevToolbox.Tools.VarCompare.Core.Steps;

/// <summary>
/// Les clés sous lesquelles chaque étape laisse son résultat à la suivante.
/// </summary>
/// <remarks>
/// Tout ce qui est déposé sous ces clés ne vit en mémoire que le temps de l'exécution, pas davantage. En
/// particulier, <see cref="Snapshots"/> contient des valeurs de variables : c'est précisément pourquoi le
/// contexte n'est jamais sérialisé et pourquoi le point de reprise est construit à partir d'un type
/// distinct, volontairement étroit.
/// </remarks>
public static class VarCompareContextKeys
{
    /// <summary>Le projet confirmé.</summary>
    public const string Project = "varcompare.project";

    /// <summary>Les groupes lisibles dans ce projet.</summary>
    public const string AvailableGroups = "varcompare.groups.available";

    /// <summary>Les groupes que le développeur a choisi de comparer.</summary>
    public const string SelectedGroups = "varcompare.groups.selected";

    /// <summary>Les clichés déjà obtenus, indexés par id de groupe. Jamais conservés sur disque.</summary>
    public const string Snapshots = "varcompare.snapshots";

    /// <summary>La sélection restaurée d'une exécution interrompue, avant rapprochement.</summary>
    public const string ResumedSelection = "varcompare.resume.selection";

    /// <summary>La comparaison construite.</summary>
    public const string Comparison = "varcompare.comparison";
}
