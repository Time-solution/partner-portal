namespace Zahy.Commission;

/// <summary>Neutral order monetary snapshot for commission basis resolution.</summary>
public sealed class CommissionOrderAmounts
{
    /// <summary>Merchandise subtotal — default commission basis (excludes tax and delivery).</summary>
    public decimal Subtotal { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal DeliveryFee { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = CommissionConsts.DefaultCurrency;

    public IReadOnlyDictionary<string, decimal>? LineSubtotalsBySku { get; init; }
}
