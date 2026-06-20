namespace Zahy.Settlement;

/// <summary>
/// Aggregates output and input VAT over a period/invoice and applies the NEGATIVE-VAT GUARD:
/// a tax invoice never shows negative VAT. When input VAT exceeds output VAT (a payout/refund-dominant
/// period), the invoice VAT due is clamped to zero and the excess is carried as an input-VAT credit
/// (reclaim). The exact reclaim treatment is an accountant decision (DESIGN.md §9 / §11.4).
/// </summary>
public sealed class SettlementVatSummary
{
    public Money OutputVat { get; }

    public Money InputVat { get; }

    private SettlementVatSummary(Money outputVat, Money inputVat)
    {
        OutputVat = outputVat;
        InputVat = inputVat;
    }

    /// <summary>Both arguments are net (VAT-exclusive) money in the same currency.</summary>
    public static SettlementVatSummary Of(Money outputVat, Money inputVat) =>
        new(outputVat, inputVat);

    /// <summary>Raw output − input (may be negative). NOT what is reported on a tax invoice.</summary>
    public Money RawNetVat => OutputVat - InputVat;

    public bool IsPayoutDominant => InputVat.Amount > OutputVat.Amount;

    /// <summary>VAT due on the tax invoice — guarded to never be negative.</summary>
    public Money InvoiceVatDue =>
        IsPayoutDominant ? Money.Zero(OutputVat.Currency) : RawNetVat;

    /// <summary>Excess input VAT carried as a reclaim/credit when the period is payout-dominant.</summary>
    public Money CarriedInputVatCredit =>
        IsPayoutDominant ? InputVat - OutputVat : Money.Zero(OutputVat.Currency);
}
