using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Zahy.Webhooks;

namespace Zahy.OrderLedger;

public interface IOrderLedgerWebhookNotifier
{
    Task NotifyIngestedAsync(
        OrderRecord record,
        OrderStatus? previousStatus,
        CancellationToken cancellationToken = default);
}

public class OrderLedgerWebhookNotifier : IOrderLedgerWebhookNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IWebhookOutboxPublisher _webhookOutboxPublisher;

    public OrderLedgerWebhookNotifier(IWebhookOutboxPublisher webhookOutboxPublisher)
    {
        _webhookOutboxPublisher = webhookOutboxPublisher;
    }

    public async Task NotifyIngestedAsync(
        OrderRecord record,
        OrderStatus? previousStatus,
        CancellationToken cancellationToken = default)
    {
        if (record.PartnerId == null)
        {
            return;
        }

        var partnerId = record.PartnerId.Value;
        var payloadJson = BuildPayloadJson(record);

        var sourceKey = BuildSourceKey(record);

        await _webhookOutboxPublisher.EnqueueAsync(new WebhookEnqueueRequest
        {
            PartnerId = partnerId,
            EventType = WebhookEventTypes.OrderCreated,
            IdempotencyKey = $"order.created:{sourceKey}",
            PayloadJson = payloadJson
        }, cancellationToken);

        if (record.PaymentStatus == PaymentStatus.Paid)
        {
            await _webhookOutboxPublisher.EnqueueAsync(new WebhookEnqueueRequest
            {
                PartnerId = partnerId,
                EventType = WebhookEventTypes.OrderPaid,
                IdempotencyKey = $"order.paid:{sourceKey}",
                PayloadJson = payloadJson
            }, cancellationToken);
        }

        if (previousStatus.HasValue && previousStatus.Value != record.Status)
        {
            var statusPayload = BuildStatusChangedPayload(record, previousStatus.Value);
            await _webhookOutboxPublisher.EnqueueAsync(new WebhookEnqueueRequest
            {
                PartnerId = partnerId,
                EventType = WebhookEventTypes.OrderStatusChanged,
                IdempotencyKey = $"order.status_changed:{sourceKey}",
                PayloadJson = statusPayload
            }, cancellationToken);
        }
    }

    private static string BuildSourceKey(OrderRecord record) =>
        $"{record.SourceSystem}:{record.SourceOrderId}:{record.SourceVersion}";

    private static string BuildPayloadJson(OrderRecord record)
    {
        var payload = new
        {
            orderRecordId = record.Id,
            sourceSystem = record.SourceSystem,
            sourceOrderId = record.SourceOrderId,
            sourceVersion = record.SourceVersion,
            tenantId = record.TenantId,
            partnerId = record.PartnerId,
            direction = record.Direction.ToString(),
            status = record.Status.ToString(),
            paymentStatus = record.PaymentStatus.ToString(),
            totalAmount = record.TotalAmount,
            currency = record.Currency
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string BuildStatusChangedPayload(OrderRecord record, OrderStatus previousStatus)
    {
        var payload = new
        {
            orderRecordId = record.Id,
            sourceSystem = record.SourceSystem,
            sourceOrderId = record.SourceOrderId,
            sourceVersion = record.SourceVersion,
            tenantId = record.TenantId,
            partnerId = record.PartnerId,
            direction = record.Direction.ToString(),
            previousStatus = previousStatus.ToString(),
            newStatus = record.Status.ToString(),
            paymentStatus = record.PaymentStatus.ToString(),
            totalAmount = record.TotalAmount,
            currency = record.Currency
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
