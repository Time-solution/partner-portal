namespace Zahy.Webhooks;

public enum WebhookOutboxStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    DeadLettered = 4
}
