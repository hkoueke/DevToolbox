using DevToolbox.Application.Abstractions;

namespace DevToolbox.Presentation.Shell;

/// <summary>La réponse du développeur à l'invite de reprise.</summary>
/// <param name="Decision">Ce qu'il a choisi.</param>
/// <param name="Run">L'exécution concernée, ou <see langword="null"/> s'il n'y en avait aucune.</param>
public sealed record ResumeAnswer(ResumeDecision Decision, PersistedRun? Run);
