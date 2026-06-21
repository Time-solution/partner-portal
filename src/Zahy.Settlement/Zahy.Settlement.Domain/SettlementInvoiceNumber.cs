using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Deterministic invoice-number scheme: <c>ZH-{YYYYMM}-{entityCode}-{seq}</c>. Stable for the same
/// entity+period+sequence. BETA until ZATCA — this is NOT yet a compliant tax-invoice number and must
/// not be treated as one.
/// </summary>
public static class SettlementInvoiceNumber
{
    public static string For(string entityCode, SettlementPeriod period, int sequence = 1)
    {
        if (string.IsNullOrWhiteSpace(entityCode))
        {
            throw new AbpException("Invoice entity code is required.");
        }

        Check.NotNull(period, nameof(period));

        var code = entityCode.Trim().ToUpperInvariant();
        return $"{SettlementInvoiceConsts.InvoiceNumberPrefix}-{period.Year:D4}{period.Month:D2}-{code}-{sequence:D2}";
    }
}
