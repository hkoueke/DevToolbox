namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Les étapes nommées d'une exécution d'apidelta, et celles que chaque mode traverse.
/// </summary>
/// <remarks>
/// L'ordre est fixé ici parce que c'est lui que l'invite de reprise et le rapport citent. Un mode ne
/// réordonne jamais les étapes : il en saute. Une étape sautée est consignée comme sautée, jamais comme
/// réussie — l'exécution rend ainsi un compte véridique de ce qui a eu lieu.
/// <para>
/// Toutes les étapes ne sont pas encore écrites. Celles qui existent sont déclarées à l'exécution ; les
/// autres rejoindront la liste avec la tranche qui les implémente, afin qu'une exécution ne comporte
/// jamais d'étape qui ne fait rien.
/// </para>
/// </remarks>
public static class ApiDeltaStepNames
{
    /// <summary>Choisir ce qu'il faut lire, et ce qu'il faut en faire.</summary>
    public const string Select = "Choix des fichiers";

    /// <summary>Lire le fichier de production.</summary>
    public const string ReadTarget = "Lecture de votre production";

    /// <summary>Lire le fichier compagnon, quand c'est une description OpenAPI.</summary>
    public const string ReadSource = "Lecture du fichier compagnon";

    /// <summary>La lecture d'un contrat de service WSDL, par chemin ou par adresse.</summary>
    public const string ReadWsdl = "Lecture du contrat de service";

    /// <summary>L'appariement des opérations du contrat avec les routes de la production.</summary>
    public const string Correspond = "Appariement des opérations";

    /// <summary>Le développeur dit ce que chaque opération retenue devient comme route.</summary>
    /// <remarks>
    /// Nommé par ce que le développeur y fait, et non par ce que l'outil y calcule : il y compose des
    /// routes. La dérivation d'un itinéraire depuis un nom d'opération n'y est plus qu'une suggestion.
    /// </remarks>
    public const string Author = "Composition des routes";

    /// <summary>Valider la structure et résoudre les références.</summary>
    public const string Validate = "Vérification des fichiers";

    /// <summary>Comparer les deux descriptions.</summary>
    public const string Compare = "Comparaison";

    /// <summary>Recommander la version à publier.</summary>
    public const string Version = "Numéro de version à publier";

    /// <summary>Auditer la production.</summary>
    public const string Audit = "Examen du fichier";

    /// <summary>Établir ce que le nettoyage rapporterait.</summary>
    public const string Plan = "Chiffrage du nettoyage";

    /// <summary>Choisir ce qui entre dans la production consolidée, et la construire.</summary>
    public const string Correct = "Ce qui entre dans la production consolidée";

    /// <summary>Écrire les artefacts.</summary>
    public const string Write = "Écriture des rapports";

    /// <summary>Toutes les étapes, dans l'ordre d'exécution.</summary>
    public static IReadOnlyList<string> InOrder { get; } =
    [
        Select, ReadTarget, ReadWsdl, ReadSource, Correspond, Author, Validate, Compare, Version,
        Audit, Plan, Correct, Write,
    ];

    /// <summary>Indique si un mode traverse une étape.</summary>
    /// <param name="step">Le nom de l'étape.</param>
    /// <param name="mode">Le mode de l'exécution.</param>
    /// <returns><see langword="false"/> si l'étape doit être sautée pour ce mode.</returns>
    public static bool Includes(string step, RunMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step);

        return step switch
        {
            // Tout ce qui demande un fichier compagnon : sa lecture, son appariement s'il est un
            // contrat de service, la comparaison, la version à publier, et ce qui entre dans la
            // production consolidée.
            ReadWsdl or ReadSource or Correspond or Author or Compare or Version or Correct =>
                mode is RunMode.Consolidate,

            Audit or Plan => mode is RunMode.Analyse,
            _ => true,
        };
    }

    /// <summary>La raison pour laquelle une étape est sautée, telle qu'elle s'affiche.</summary>
    /// <param name="mode">Le mode de l'exécution.</param>
    /// <returns>Une raison déjà affichable.</returns>
    /// <remarks>
    /// La raison vit avec l'outil et non sur l'état d'étape partagé : seul l'outil sait ce qu'un mode
    /// signifie, et la couche partagée n'a pas à apprendre le mot « audit ».
    /// </remarks>
    public static string SkipReason(RunMode mode) =>
        "sans objet pour une " + RunModeNames.Of(mode);
}
