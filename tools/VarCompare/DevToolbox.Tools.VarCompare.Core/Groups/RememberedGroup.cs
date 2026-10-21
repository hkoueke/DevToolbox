namespace DevToolbox.Tools.VarCompare.Core.Groups;

/// <summary>
/// Un groupe de variables mémorisé au sein d'une exécution interrompue : son identité seule, jamais son
/// contenu.
/// </summary>
/// <param name="Id">L'identifiant numérique du groupe, par lequel il sera relu.</param>
/// <param name="Name">Le nom du groupe, pour que l'invite de reprise puisse le citer sans aller-retour.</param>
/// <remarks>
/// Ce type vit dans l'outil et non dans la couche partagée : un point de reprise ne sait rien des groupes
/// de variables, il ne porte que des chaînes. La conversion a lieu à la frontière de varcompare, là où
/// « 42 » redevient un identifiant de groupe parce que c'est là que ce mot veut dire quelque chose.
/// </remarks>
public sealed record RememberedGroup(int Id, string Name);
