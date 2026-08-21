using System.Net;
using System.Text;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Répond aux requêtes HTTP à partir d'un scénario en file d'attente, et consigne ce qui a été demandé.
/// </summary>
/// <remarks>
/// Consigner le verbe de chaque requête est ce qui permet à un test de vérifier la garantie de lecture seule
/// sur du trafic réel plutôt que sur le code source.
/// </remarks>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    internal List<HttpRequestMessage> Requests { get; } = [];

    internal FakeHttpMessageHandler Enqueue(HttpStatusCode status, string? json = null, string? continuationToken = null)
    {
        HttpResponseMessage response = new(status);

        if (json is not null)
        {
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (continuationToken is not null)
        {
            response.Headers.Add("x-ms-continuationtoken", continuationToken);
        }

        _responses.Enqueue(response);

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        return Task.FromResult(
            _responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (HttpResponseMessage response in _responses)
            {
                response.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
