namespace Zahy.Settlement;

/// <summary>
/// How an inbound payment receipt was tendered. Replaces the old free-text method so a recorded
/// receipt REMEMBERS how it was paid. Mirrors the frontend <c>InvoicePayment.method</c>
/// ("COD" | "Online" | "Transfer") and extends it with the other manual tenders.
///
/// <see cref="Gateway"/> is a LABEL only — the gateway leg remains the unwired
/// <see cref="GatewayAutoRouteSeam"/> seam; recording <see cref="Gateway"/> here neither wires a real
/// gateway nor auto-reconciles anything.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    Transfer = 2,
    Card = 3,
    Cod = 4,
    Online = 5,
    Gateway = 6
}
