using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Infrastructure.Wire;

/// <summary>Convertit le format transmis vers le modèle métier.</summary>
public static class VariableGroupMapper
{
    private const string KeyVaultType = "AzureKeyVault";

    /// <summary>Convertit un groupe en son résumé de liste.</summary>
    /// <param name="dto">Le groupe transmis.</param>
    /// <returns>Le résumé.</returns>
    public static VariableGroupSummary ToSummary(VariableGroupDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new VariableGroupSummary(
            dto.Id,
            dto.Name,
            dto.Description,
            ToOrigin(dto.Type),
            dto.IsShared);
    }

    /// <summary>Convertit un groupe entièrement lu en cliché.</summary>
    /// <param name="dto">Le groupe transmis.</param>
    /// <param name="retrievedAt">Quand le groupe a été lu, selon l'horloge injectée.</param>
    /// <returns>Le cliché.</returns>
    public static VariableGroupSnapshot ToSnapshot(VariableGroupDto dto, DateTimeOffset retrievedAt)
    {
        ArgumentNullException.ThrowIfNull(dto);

        VariableGroupSummary summary = ToSummary(dto);
        IEnumerable<VariableEntry> entries = dto.Variables is null
            ? []
            : dto.Variables.Select(pair => ToEntry(pair.Key, pair.Value));

        return new VariableGroupSnapshot(summary, entries, retrievedAt, dto.ModifiedOn);
    }

    /// <summary>Convertit le discriminant <c>type</c> transmis en origine de groupe.</summary>
    /// <param name="type">La valeur transmise.</param>
    /// <returns>L'origine.</returns>
    public static VariableGroupOrigin ToOrigin(string? type) =>
        string.Equals(type, KeyVaultType, StringComparison.OrdinalIgnoreCase)
            ? VariableGroupOrigin.KeyVaultBacked
            : VariableGroupOrigin.Ordinary;

    private static VariableEntry ToEntry(string name, VariableDto? dto) =>
        new(
            name,
            dto?.Value,
            dto?.IsSecret ?? false,
            dto?.IsReadOnly ?? false);
}
