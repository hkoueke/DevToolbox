namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>L'état d'une étape, tel que le rapport le cite.</summary>
/// <param name="Name">Le nom de l'étape.</param>
/// <param name="Status">Ce qu'il en est advenu, dans le vocabulaire de la boîte à outils.</param>
/// <param name="SkipReason">Pourquoi elle a été sautée, le cas échéant.</param>
/// <remarks>
/// Une étape sautée porte sa raison, parce qu'un rapport qui passe une étape sous silence la donne pour
/// accomplie. La raison vit ici plutôt que sur l'état d'étape partagé : seul l'outil sait ce qu'un mode
/// veut dire.
/// </remarks>
public sealed record RunStepState(string Name, string Status, string? SkipReason = null);
