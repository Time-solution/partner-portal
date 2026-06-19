namespace Zahy.Webhooks;

public static class WebhookEventTypes
{
    public const string OrderCreated = "order.created";
    public const string OrderPaid = "order.paid";
    public const string OrderStatusChanged = "order.status_changed";
    public const string InventoryChanged = "inventory.changed";
    public const string PartnerApproved = "partner.approved";
    public const string PartnerSuspended = "partner.suspended";

    public static readonly string[] All =
    [
        OrderCreated,
        OrderPaid,
        OrderStatusChanged,
        InventoryChanged,
        PartnerApproved,
        PartnerSuspended
    ];
}
