namespace DevToolbox.Application.Abstractions;

/// <summary>Identité de la session dont les choix ont été repris, sans données métier.</summary>
public static class CheckpointContextKeys
{
    /// <summary>Identifiant du point de reprise à retirer quand la session finit ou recommence.</summary>
    public const string SourceRunId = "checkpoint.sourceRunId";

    /// <summary>Le stockage n'a pas permis d'oublier la session ; l'utilisateur doit en être averti.</summary>
    public const string CleanupFailed = "checkpoint.cleanupFailed";
}
