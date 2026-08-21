namespace DevToolbox.Domain.Results;

/// <summary>L'issue d'une opération qui produit une valeur lorsqu'elle réussit.</summary>
/// <typeparam name="T">Le type de la valeur produite en cas de succès.</typeparam>
/// <remarks>
/// Les instances se créent par les fabriques du type non générique <see cref="Result"/>
/// (<see cref="Result.Success{T}(T)"/>, <see cref="Result.Fail{T}(Failure)"/>) plutôt que par des membres
/// statiques déclarés ici : un membre statique sur un type générique est un piège d'utilisation, et l'éviter
/// dispense d'une suppression d'analyseur.
/// </remarks>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, Failure? failure)
        : base(failure) => _value = value;

    /// <summary>La valeur produite en cas de succès.</summary>
    /// <exception cref="InvalidOperationException">L'issue est un échec.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot read the value of a failed result ({Failure!.Reason}).");

    /// <summary>Lit la valeur lorsque l'opération a réussi.</summary>
    /// <param name="value">La valeur produite, ou <see langword="default"/> en cas d'échec.</param>
    /// <returns><see langword="true"/> lorsque l'opération a réussi.</returns>
    public bool TryGetValue(out T? value)
    {
        value = IsSuccess ? _value : default;
        return IsSuccess;
    }
}
