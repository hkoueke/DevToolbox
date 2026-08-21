namespace DevToolbox.Domain.Results;

/// <summary>
/// L'issue d'une opération susceptible d'échouer pour une raison prévue. Les ports renvoient ce type plutôt
/// que de lever une exception, afin qu'un refus d'accès ou un serveur injoignable soit un branchement
/// ordinaire et non une exception.
/// </summary>
public class Result
{
    /// <summary>Initialise une issue.</summary>
    /// <param name="failure">L'échec, ou <see langword="null"/> lorsque l'issue est un succès.</param>
    protected Result(Failure? failure) => Failure = failure;

    /// <summary>L'échec, ou <see langword="null"/> lorsque <see cref="IsSuccess"/> vaut vrai.</summary>
    public Failure? Failure { get; }

    /// <summary>Indique si l'opération a réussi.</summary>
    public bool IsSuccess => Failure is null;

    /// <summary>Indique si l'opération a échoué.</summary>
    public bool IsFailure => Failure is not null;

    /// <summary>Crée une issue réussie.</summary>
    /// <returns>Un succès.</returns>
    public static Result Success() => new(null);

    /// <summary>Crée une issue réussie porteuse d'une valeur.</summary>
    /// <typeparam name="T">Le type de la valeur.</typeparam>
    /// <param name="value">La valeur produite.</param>
    /// <returns>Un succès.</returns>
    public static Result<T> Success<T>(T value) => new(value, null);

    /// <summary>Crée une issue en échec.</summary>
    /// <param name="reason">La catégorie d'échec.</param>
    /// <param name="message">Un message affichable tel quel.</param>
    /// <returns>Un échec.</returns>
    public static Result Fail(FailureReason reason, string message) =>
        new(Results.Failure.Of(reason, message));

    /// <summary>Crée une issue en échec à partir d'un échec existant.</summary>
    /// <param name="failure">L'échec à porter.</param>
    /// <returns>Un échec.</returns>
    public static Result Fail(Failure failure) => new(failure);

    /// <summary>Crée l'échec d'une opération censée produire une valeur.</summary>
    /// <typeparam name="T">Le type que l'opération aurait produit.</typeparam>
    /// <param name="reason">La catégorie d'échec.</param>
    /// <param name="message">Un message affichable tel quel.</param>
    /// <returns>Un échec.</returns>
    public static Result<T> Fail<T>(FailureReason reason, string message) =>
        new(default, Results.Failure.Of(reason, message));

    /// <summary>Crée l'échec d'une opération à valeur, à partir d'un échec existant.</summary>
    /// <typeparam name="T">Le type que l'opération aurait produit.</typeparam>
    /// <param name="failure">L'échec à porter.</param>
    /// <returns>Un échec.</returns>
    public static Result<T> Fail<T>(Failure failure) => new(default, failure);
}
