namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Un groupe de variables mémorisé au sein d'une exécution interrompue : son identité seule, jamais son
/// contenu.
/// </summary>
/// <param name="Id">L'identifiant numérique du groupe.</param>
/// <param name="Name">Le nom du groupe, pour que l'invite de reprise puisse le citer sans aller-retour.</param>
public sealed record PersistedGroupSelection(int Id, string Name);
