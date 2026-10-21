namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>À partir de quoi une exécution est tenue pour un échec.</summary>
public enum FailureThreshold
{
    /// <summary>Jamais : les constats sont rapportés, l'exécution reste un succès.</summary>
    Never,

    /// <summary>Dès un avertissement.</summary>
    OnWarning,

    /// <summary>Dès une erreur.</summary>
    OnError,

    /// <summary>Dès un changement cassant.</summary>
    OnBreakingChange,
}
