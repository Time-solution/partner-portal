using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Webhooks;

/// <summary>In-memory transport for tests and local dev — no real HTTP.</summary>
public class InMemoryWebhookDeliveryTransport : IWebhookDeliveryTransport
{
    private readonly ConcurrentQueue<WebhookDeliveryRequest> _requests = new();
    private Func<WebhookDeliveryRequest, WebhookTransportResult>? _handler;

    public IReadOnlyCollection<WebhookDeliveryRequest> Requests => _requests.ToArray();

    public void ConfigureHandler(Func<WebhookDeliveryRequest, WebhookTransportResult> handler) =>
        _handler = handler;

    public void Reset()
    {
        while (_requests.TryDequeue(out _))
        {
        }

        _handler = null;
    }

    public Task<WebhookTransportResult> SendAsync(
        WebhookDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        _requests.Enqueue(request);

        if (_handler != null)
        {
            return Task.FromResult(_handler(request));
        }

        return Task.FromResult(new WebhookTransportResult
        {
            Success = true,
            IsRetryable = false,
            HttpStatusCode = 200,
            ResponseBody = "OK",
            DurationMs = 1
        });
    }
}
