using System.Net;
using DevToolbox.Domain.Results;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Traduit la réalité du transport en issues métier. Aucun code de statut, aucune URL, aucun corps de
/// réponse ni aucune trace d'appel ne s'échappe de cette classe.
/// </summary>
public static class AzureDevOpsFailures
{
    /// <summary>Fait correspondre un code de statut HTTP à un échec métier.</summary>
    /// <param name="statusCode">Le statut renvoyé par le serveur.</param>
    /// <param name="serverHost">L'hôte du serveur, pour le message d'authentification.</param>
    /// <param name="collection">La collection, pour le message d'authentification.</param>
    /// <returns>L'échec correspondant.</returns>
    public static Failure FromStatusCode(HttpStatusCode statusCode, string serverHost, string collection) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => Failure.Of(
                FailureReason.AuthenticationFailed,
                $"The server rejected your Windows credentials. Server: {serverHost}. "
                + $"Collection: {collection}. Check that you are on the corporate network and that your "
                + "account has access."),

            HttpStatusCode.Forbidden => Failure.Of(
                FailureReason.PermissionDenied,
                "You do not have permission to read this. The tool works strictly within your own rights."),

            HttpStatusCode.NotFound => Failure.Of(
                FailureReason.GroupNotFound,
                "The server reports that this no longer exists."),

            HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => Failure.Of(
                FailureReason.ServiceUnavailable,
                "The service is throttling or unavailable. The tool waited as instructed and gave up."),

            _ when IsRedirect(statusCode) => Failure.Of(
                FailureReason.RedirectRefused,
                "The server answered with a redirect, which was refused so that your Windows credentials "
                + "are never presented to another host."),

            _ => Failure.Of(
                FailureReason.ServiceUnavailable,
                "The server returned an unexpected response and the request could not be completed."),
        };

    /// <summary>Fait correspondre une exception de transport à un échec métier.</summary>
    /// <param name="exception">L'exception levée par la pile HTTP.</param>
    /// <returns>L'échec correspondant.</returns>
    public static Failure FromTransportException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (ContainsAuthenticationFailure(exception))
        {
            return Failure.Of(
                FailureReason.ServerUntrusted,
                "The certificate presented by the server could not be validated. Certificate validation is "
                + "never disabled by this tool.");
        }

        return Failure.Of(
            FailureReason.ServerUnreachable,
            "The server could not be reached. Are you on the corporate network, or is the VPN down?");
    }

    /// <summary>L'échec employé quand le budget de reprises est épuisé ou le disjoncteur ouvert.</summary>
    /// <returns>L'échec.</returns>
    public static Failure ServiceUnavailable() => Failure.Of(
        FailureReason.ServiceUnavailable,
        "The server appears to be unavailable. The tool stopped retrying rather than continuing to call it.");

    /// <summary>L'échec employé quand le développeur annule.</summary>
    /// <returns>L'échec.</returns>
    public static Failure Cancelled() => Failure.Of(
        FailureReason.Cancelled,
        "The operation was cancelled.");

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        (int)statusCode is >= 300 and < 400;

    private static bool ContainsAuthenticationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is System.Security.Authentication.AuthenticationException)
            {
                return true;
            }
        }

        return false;
    }
}
