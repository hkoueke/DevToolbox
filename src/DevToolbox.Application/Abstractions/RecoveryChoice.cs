namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Ce que le développeur a choisi de faire d'une étape en échec. Les quatre possibilités lui sont toujours
/// proposées dans cet ordre.
/// </summary>
public enum RecoveryChoice
{
    /// <summary>Relancer l'étape en échec.</summary>
    RetryStep = 0,

    /// <summary>Poursuivre au-delà de l'étape en échec, en conservant tout ce qui est déjà acquis.</summary>
    ContinueFromStep,

    /// <summary>Reprendre tout l'outil depuis le début, en jetant l'état conservé.</summary>
    RestartAll,

    /// <summary>Renoncer, en laissant l'exécution dans un état décrit.</summary>
    Abort,
}
