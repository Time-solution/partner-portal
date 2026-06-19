namespace Zahy.Webhooks;

public static class WebhookErrorCodes
{
    public const string Namespace = "Zahy.Webhooks";

    public const string InvalidEventType = Namespace + ":InvalidEventType";
    public const string InvalidTargetUrl = Namespace + ":InvalidTargetUrl";
    public const string SubscriptionNotFound = Namespace + ":SubscriptionNotFound";
    public const string DuplicateTargetUrl = Namespace + ":DuplicateTargetUrl";
    public const string OutboxNotFound = Namespace + ":OutboxNotFound";
    public const string DeadLetterNotFound = Namespace + ":DeadLetterNotFound";
}
