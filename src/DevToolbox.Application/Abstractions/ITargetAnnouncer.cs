namespace DevToolbox.Application.Abstractions;

/// <summary>Annonce la cible avant toute lecture.</summary>
public interface ITargetAnnouncer
{
    /// <summary>Annonce le serveur et la collection sur le point d'être lus.</summary>
    /// <param name="target">La cible annoncée.</param>
    void AnnounceTarget(TargetAnnouncement target);
}
