namespace Zahy.Settlement;

/// <summary>The VAT + margin breakdown for one cost/markup line under a given treatment. All amounts are net (VAT-exclusive) money except where named otherwise.</summary>
public sealed record ResaleVatResult
{
    public VatTreatment Treatment { get; init; }

    public Money NetBuy { get; init; } = Money.Zero();

    public Money NetSell { get; init; } = Money.Zero();

    public Money InputVat { get; init; } = Money.Zero();

    public Money OutputVat { get; init; } = Money.Zero();

    public Money Margin { get; init; } = Money.Zero();

    /// <summary>Output VAT − input VAT for this line (what the platform owes ZATCA for it).</summary>
    public Money NetVatToZatca { get; init; } = Money.Zero();
}
