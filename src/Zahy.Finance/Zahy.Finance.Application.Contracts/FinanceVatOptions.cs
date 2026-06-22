namespace Zahy.Finance;

/// <summary>
/// VAT configuration for Zahy.Finance documents (bound from the "Finance:Vat" section; default 0.15 = KSA
/// standard rate). The rate is applied as a VAT-INCLUSIVE back-out (see <see cref="FinanceVat"/>), the SAME
/// convention as the Settlement engine — so Finance and Settlement split an identical inclusive amount into
/// an identical net + VAT. Single source of the Finance VAT rate (replaces the previously hard-coded 0.15m).
/// </summary>
public class FinanceVatOptions
{
    public decimal StandardRate { get; set; } = 0.15m;
}
