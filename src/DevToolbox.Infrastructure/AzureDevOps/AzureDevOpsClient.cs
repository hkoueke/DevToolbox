namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Le client typé du serveur Azure DevOps sur site. Obtenu auprès de <c>IHttpClientFactory</c>, jamais
/// construit directement : la liste d'API interdites fait échouer la compilation dans le cas contraire.
/// </summary>
public sealed class AzureDevOpsClient
{
    /// <summary>Crée le client typé.</summary>
    /// <param name="httpClient">Le client configuré, fourni par la fabrique de clients HTTP.</param>
    public AzureDevOpsClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        HttpClient = httpClient;
    }

    /// <summary>Le client sous-jacent, dont l'adresse de base se termine déjà par une barre oblique.</summary>
    public HttpClient HttpClient { get; }
}
