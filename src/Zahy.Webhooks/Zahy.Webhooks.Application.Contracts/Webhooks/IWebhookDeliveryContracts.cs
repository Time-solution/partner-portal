using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.Webhooks;

public class WebhookEnqueueRequest
{
    public Guid PartnerId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;
}

public interface IWebhookOutboxPublisher : IApplicationService
{
    Task<Guid> EnqueueAsync(WebhookEnqueueRequest request, CancellationToken cancellationToken = default);
}

public class WebhookDeliveryRequest
{
    public Guid DeliveryId { get; set; }

    public Guid SubscriptionId { get; set; }

    public string TargetUrl { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public long UnixTimestamp { get; set; }

    public string Signature { get; set; } = string.Empty;
}

public class WebhookTransportResult
{
    public bool Success { get; set; }

    public bool IsRetryable { get; set; }

    public int? HttpStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorCode { get; set; }

    public int DurationMs { get; set; }
}

public interface IWebhookDeliveryTransport
{
    Task<WebhookTransportResult> SendAsync(
        WebhookDeliveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWebhookDeliveryProcessor : IApplicationService
{
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}
