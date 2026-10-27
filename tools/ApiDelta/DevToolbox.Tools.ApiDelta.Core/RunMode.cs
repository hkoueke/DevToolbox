namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Ce qu'une exécution d'apidelta est censée produire.</summary>
/// <remarks>
/// Deux modes, nommés d'après ce que la personne devant l'écran vient faire, et non d'après ce que
/// l'outil fait à l'intérieur. Il y en avait six : comparer, corriger, examiner, chiffrer le nettoyage,
/// extraire une route, et tout faire. Ce n'étaient pas six intentions mais deux, découpées selon les
/// étapes du code — comparer seul est ce que consolider fait en chemin, et son rapport le dit ; examiner
/// et chiffrer sont les deux moitiés d'une question que personne ne pose en deux fois.
/// <para>
/// L'extraction d'une route dans une description autonome a été retirée, et non cachée : elle répondait à
/// une question qu'on n'ouvre pas cet outil pour poser.
/// </para>
/// </remarks>
public enum RunMode
{
    /// <summary>
    /// Consolider la production : elle, plus ce que le développeur retient du fichier compagnon.
    /// </summary>
    Consolidate,

    /// <summary>
    /// Analyser un fichier seul : ce qu'il a accumulé, et ce qu'un nettoyage lui gagnerait.
    /// </summary>
    Analyse,
}
