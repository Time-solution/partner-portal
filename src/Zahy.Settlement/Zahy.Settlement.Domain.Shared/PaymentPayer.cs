namespace Zahy.Settlement;

/// <summary>
/// Who pays Zahy when money comes IN (inbound payment-received only). Selects which receivable
/// the cash receipt clears: Merchant → credit 1200 AR-Merchant, Partner → credit 1250 AR-Partner.
/// Mirrors the frontend mock payer ("Merchant" | "Partner").
/// </summary>
public enum PaymentPayer
{
    Merchant = 1,
    Partner = 2
}
