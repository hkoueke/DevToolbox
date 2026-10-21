using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Fait tourner les règles d'audit sur une description.</summary>
public interface IAuditEngine
{
    /// <summary>Audite une description.</summary>
    /// <param name="request">Ce qu'il faut pour auditer.</param>
    /// <param name="cancellationToken">L'annulation.</param>
    /// <returns>Les constats, et le sort de chaque règle.</returns>
    AuditResult Run(AuditRequest request, CancellationToken cancellationToken);
}
