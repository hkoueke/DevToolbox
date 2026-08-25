namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>
/// Les largeurs que la présentation occupera réellement, mesurées sur ce qu'elle s'apprête à afficher.
/// </summary>
/// <remarks>
/// <para>
/// Le cœur ne connaît ni les symboles, ni les mots, ni les qualificatifs qui composent une cellule : ils
/// appartiennent à la présentation. Réserver à leur place une largeur maximale ferait basculer vers
/// l'affichage empilé des comparaisons qui tiennent très bien en colonnes — une colonne de « définie »
/// n'occupe pas la place d'une colonne d'« indéterminée [kv] [ro] ». La présentation mesure donc ses
/// propres libellés et transmet le résultat ici.
/// </para>
/// <para>
/// Les largeurs sont celles du <em>contenu</em>, hors bordure et marge : le sélecteur ajoute lui-même ce
/// que le tableau consomme autour de chaque colonne.
/// </para>
/// </remarks>
/// <param name="NameColumn">La largeur du plus large texte de la colonne des noms, marques comprises.</param>
/// <param name="GroupColumns">
/// La largeur de chaque colonne de groupe, dans l'ordre des groupes de la comparaison.
/// </param>
public sealed record LayoutMetrics(int NameColumn, IReadOnlyList<int> GroupColumns);
