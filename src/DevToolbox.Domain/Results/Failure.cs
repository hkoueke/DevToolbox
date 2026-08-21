namespace DevToolbox.Domain.Results;

/// <summary>
/// Un échec : une raison sur laquelle l'appelant peut brancher, et un message déjà présentable à un
/// développeur.
/// </summary>
/// <param name="Reason">La catégorie d'échec.</param>
/// <param name="Message">
/// Un message affichable tel quel. Les implémentations ne doivent y placer ni trace d'appel, ni corps de
/// réponse brut, ni URL complète avec sa chaîne de requête, ni la moindre donnée d'authentification.
/// </param>
public sealed record Failure(FailureReason Reason, string Message)
{
    /// <summary>Crée un échec à partir d'une raison et d'un message.</summary>
    /// <param name="reason">La catégorie d'échec.</param>
    /// <param name="message">Un message affichable tel quel.</param>
    /// <returns>L'échec.</returns>
    public static Failure Of(FailureReason reason, string message) => new(reason, message);
}
