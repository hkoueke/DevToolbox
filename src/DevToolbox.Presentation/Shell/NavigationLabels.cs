namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Les entrées qui font naviguer plutôt que choisir : revenir en arrière, quitter.
/// </summary>
/// <remarks>
/// <para>
/// Une liste de sélection mélange sans cela « revenir en arrière » et « choisir ceci » dans la même colonne,
/// dans la même graisse : rien ne dit à l'œil que la dernière ligne n'est pas un projet de plus. La flèche
/// est là pour cela, et elle est écrite en ASCII parce qu'elle est la seule marque qui distingue les deux —
/// elle doit rester lisible sur un terminal qui ne restitue pas l'Unicode.
/// </para>
/// <para>
/// Ces entrées se placent en fin de liste, jamais en tête : la première ligne est celle que la touche Entrée
/// valide sans que l'on ait bougé, et ce n'est pas là qu'il faut mettre « quitter ». Les invites concernées
/// activent en contrepartie le bouclage de la liste, de sorte qu'une seule flèche haut y mène.
/// </para>
/// </remarks>
public static class NavigationLabels
{
    /// <summary>Le préfixe qui signale une entrée de navigation plutôt qu'un choix.</summary>
    public const string Prefix = "<-- ";

    /// <summary>Revenir à l'étape précédente.</summary>
    public const string Back = Prefix + "Retour";

    /// <summary>Quitter la boîte à outils.</summary>
    public const string Exit = Prefix + "Quitter";

    /// <summary>
    /// Le rappel à joindre au titre d'une invite, qui dit où trouver l'entrée de navigation et comment
    /// l'atteindre en une touche.
    /// </summary>
    public const string Hint = " [grey](en fin de liste, flèche haut pour y aller directement)[/]";
}
