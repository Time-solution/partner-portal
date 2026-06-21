namespace Zahy.Settlement;

/// <summary>
/// Who is billed for an activation fee line. Selects which receivable account the fee
/// template debits: Merchant → 1200 AR-Merchant, Partner → 1250 AR-Partner.
/// Mirrors the frontend mock <c>ActivationFeePayer</c> ("Merchant" | "Partner").
/// </summary>
public enum ActivationFeePayer
{
    Merchant = 1,
    Partner = 2
}
