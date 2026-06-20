namespace Zahy.Settlement;

/// <summary>
/// The settlement chart of accounts. Names match the Phase-0 DESIGN.md account chart.
/// Final set is pending accountant sign-off (DESIGN.md §11).
/// </summary>
public enum SettlementAccountType
{
    /// <summary>Funds collected by an aggregator, held/owed to us until disbursed/reconciled.</summary>
    AggregatorClearing = 1,

    /// <summary>Money owed to the merchant (the payout liability).</summary>
    MerchantPayable = 2,

    /// <summary>Money owed to a partner (service/integration payout).</summary>
    PartnerPayable = 3,

    /// <summary>Cost of fulfilment/carrier (input/buy side).</summary>
    DeliveryCost = 4,

    /// <summary>Platform commission income.</summary>
    PlatformCommissionRevenue = 5,

    /// <summary>Margin on resold shipping (net sell minus net buy).</summary>
    ShippingMarginRevenue = 6,

    /// <summary>Output VAT we charge (liability to ZATCA).</summary>
    VatOutput = 7,

    /// <summary>Input VAT we reclaim (asset against ZATCA).</summary>
    VatInput = 8
}
