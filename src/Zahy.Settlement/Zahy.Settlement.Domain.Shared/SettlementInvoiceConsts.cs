namespace Zahy.Settlement;

public static class SettlementInvoiceConsts
{
    /// <summary>Invoice-number prefix. The number is BETA until ZATCA — NOT a compliant tax-invoice number.</summary>
    public const string InvoiceNumberPrefix = "ZH";

    /// <summary>Stamp on every generated invoice/statement until ZATCA sign-off (bilingual).</summary>
    public const string BetaLabel = "BETA / تجريبي";
}
