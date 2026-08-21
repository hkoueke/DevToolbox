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
                $"Le serveur a refusé vos identifiants Windows. Serveur : {serverHost}. "
                + $"Collection : {collection}. Vérifiez que vous êtes sur le réseau de l'entreprise et "
                + "que votre compte y a accès."),

            HttpStatusCode.Forbidden => Failure.Of(
                FailureReason.PermissionDenied,
                "Vous n'avez pas le droit de lire ceci. L'outil travaille strictement dans le cadre de vos "
                + "propres droits."),

            HttpStatusCode.NotFound => Failure.Of(
                FailureReason.GroupNotFound,
                "Le serveur indique que cela n'existe plus."),

            HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => Failure.Of(
                FailureReason.ServiceUnavailable,
                "Le service limite le débit ou est indisponible. L'outil a patienté comme demandé, puis a "
                + "renoncé."),

            _ when IsRedirect(statusCode) => Failure.Of(
                FailureReason.RedirectRefused,
                "Le serveur a répondu par une redirection, refusée afin que vos identifiants Windows ne "
                + "soient jamais présentés à un autre hôte."),

            _ => Failure.Of(
                FailureReason.ServiceUnavailable,
                "Le serveur a renvoyé une réponse inattendue et la requête n'a pas pu aboutir."),
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
                "Le certificat présenté par le serveur n'a pas pu être validé. Cet outil ne désactive "
                + "jamais la validation des certificats.");
        }

        return Failure.Of(
            FailureReason.ServerUnreachable,
            "Le serveur n'a pas pu être joint. Êtes-vous sur le réseau de l'entreprise, ou le VPN est-il "
            + "coupé ?");
    }

    /// <summary>L'échec employé quand le budget de reprises est épuisé ou le disjoncteur ouvert.</summary>
    /// <returns>L'échec.</returns>
    public static Failure ServiceUnavailable() => Failure.Of(
        FailureReason.ServiceUnavailable,
        "Le serveur semble indisponible. L'outil a cessé de réessayer plutôt que de continuer à "
        + "l'appeler.");

    /// <summary>L'échec employé quand le développeur annule.</summary>
    /// <returns>L'échec.</returns>
    public static Failure Cancelled() => Failure.Of(
        FailureReason.Cancelled,
        "L'opération a été annulée.");

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
