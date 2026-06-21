namespace Zahy.Settlement;

/// <summary>
/// How Zahy participates in a partner order — selects the posting template.
/// </summary>
public enum ParticipationMode
{
    /// <summary>Buy/sell resale: Zahy is principal. Books AR-Merchant, Resale Revenue, Output VAT,
    /// Partner COGS, Input VAT, AP-Partner.</summary>
    Principal = 1,

    /// <summary>Flat fee/subscription: books AR-Merchant, Fee Revenue, Output VAT (no cost leg).</summary>
    SubscriptionFee = 2,

    /// <summary>Pass-through reflection (e.g. HungerStation/noon): NO financial journal — only a
    /// non-posting reflection log for dashboard/POS visibility.</summary>
    ReflectionOnly = 3,

    /// <summary>Inbound cash receipt clearing a receivable (money IN): Dr 1100 Bank/Cash,
    /// Cr 1200 AR-Merchant or Cr 1250 AR-Partner. Touches no revenue/VAT/COGS account, so it
    /// contributes nothing to margin / fee revenue / net VAT — it only nets the AR down.</summary>
    PaymentReceived = 4,

    /// <summary>Outbound payout to the partner (money OUT): Dr 2100 AP-Partner, Cr 1100 Bank/Cash
    /// (a reversal posts the inverse). Touches no revenue/VAT/COGS, so it contributes nothing to
    /// margin / fee revenue / net VAT — it only nets the partner payable down.</summary>
    Disbursement = 5
}
