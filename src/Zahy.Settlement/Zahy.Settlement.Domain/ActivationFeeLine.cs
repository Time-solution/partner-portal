using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// One configurable activation fee line: a toggle, a VAT-inclusive amount, and the payer.
/// CONFIG ONLY — holding a line never posts a journal. Mirrors the frontend mock
/// <c>ActivationFeeLine</c> { enabled, amountInclusive, payer } so a live swap is drop-in.
/// </summary>
public sealed record ActivationFeeLine
{
    public bool Enabled { get; }

    /// <summary>Amount the payer is charged, always VAT-inclusive (e.g. 40 / month, 1 / txn).</summary>
    public Money AmountInclusive { get; }

    public ActivationFeePayer Payer { get; }

    private ActivationFeeLine(bool enabled, Money amountInclusive, ActivationFeePayer payer)
    {
        Enabled = enabled;
        AmountInclusive = amountInclusive;
        Payer = payer;
    }

    public static ActivationFeeLine Of(bool enabled, Money amountInclusive, ActivationFeePayer payer)
    {
        Check.NotNull(amountInclusive, nameof(amountInclusive));
        if (!amountInclusive.VatInclusive)
        {
            throw new BusinessException(SettlementVatErrorCodes.PriceMustBeVatInclusive)
                .WithData("Argument", nameof(amountInclusive));
        }

        return new ActivationFeeLine(enabled, amountInclusive, payer);
    }

    /// <summary>A disabled line (no fee) in the given currency.</summary>
    public static ActivationFeeLine Off(
        ActivationFeePayer payer = ActivationFeePayer.Merchant,
        string currency = SettlementConsts.DefaultCurrency) =>
        new(false, Money.Of(0m, currency, vatInclusive: true), payer);
}
