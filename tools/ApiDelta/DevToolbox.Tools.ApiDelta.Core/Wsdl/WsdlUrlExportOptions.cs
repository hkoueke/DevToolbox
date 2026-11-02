using DevToolbox.Tools.ApiDelta.Core.Configuration;

namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Le dossier de collecte et les autorisations explicites de récupération d'un WSDL.</summary>
/// <param name="SnapshotDirectory">Nouveau dossier où conserver sources, copie hors ligne et validation.</param>
/// <param name="AdditionalHosts">Hôtes d'import supplémentaires ; l'hôte d'entrée est déjà autorisé.</param>
/// <param name="ConfirmedAddresses">Adresses IP internes explicitement autorisées.</param>
/// <param name="AllowPlainHttp">Autorisation explicite de HTTP sans TLS.</param>
/// <param name="Limits">Budgets réduits, ou valeurs par défaut. Les plafonds de l'export local restent applicables.</param>
public sealed record WsdlUrlExportOptions(
    string SnapshotDirectory,
    IReadOnlyList<string>? AdditionalHosts = null,
    IReadOnlyList<string>? ConfirmedAddresses = null,
    bool AllowPlainHttp = false,
    WsdlLimits? Limits = null);
