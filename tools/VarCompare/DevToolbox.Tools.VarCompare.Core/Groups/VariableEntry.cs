namespace DevToolbox.Tools.VarCompare.Core.Groups;

/// <summary>Une variable au sein d'un groupe.</summary>
/// <param name="Name">Le nom tel qu'il est stocké dans ce groupe, casse comprise.</param>
/// <param name="Value">
/// La valeur lisible, ou <see langword="null"/> si la variable est secrète ou adossée à un coffre de clés.
/// Azure DevOps ne renvoie pas les valeurs secrètes : cette absence est donc le cas normal d'un secret et
/// non une anomalie.
/// </param>
/// <param name="IsSecret">Si la valeur est détenue comme secrète et ne peut pas être relue.</param>
/// <param name="IsReadOnly">Si la variable est marquée en lecture seule dans ce groupe.</param>
public sealed record VariableEntry(string Name, string? Value, bool IsSecret, bool IsReadOnly)
{
    /// <summary>Indique si la variable existe mais porte une valeur vide.</summary>
    public bool IsEmpty => Value is not null && Value.Length == 0;

    /// <summary>Indique si la variable possède une valeur réellement lisible.</summary>
    public bool HasReadableValue => Value is { Length: > 0 };
}
