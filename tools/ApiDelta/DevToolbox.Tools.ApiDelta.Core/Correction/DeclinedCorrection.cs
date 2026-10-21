namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Quelque chose que le développeur a retenu et que l'outil ne portera pas, avec la raison.</summary>
/// <param name="What">Ce qui a été retenu, sous sa forme affichable.</param>
/// <param name="Why">Pourquoi cela ne peut pas être porté.</param>
/// <remarks>
/// Écarter en silence serait la pire des deux options : le développeur a demandé quelque chose, et ne
/// pas le voir dans le résultat sans explication le laisse croire que l'outil l'a fait. Un élément écarté
/// est donc nommé, avec sa raison, au même endroit que ceux qui passent.
/// </remarks>
public sealed record DeclinedCorrection(string What, string Why);
