namespace Zahy.Finance;

public class FinanceBrandingOptions
{
    public const string SectionName = "Finance:Branding";

    public string BrandName { get; set; } = "Zahy";

    public string BrandNameArabic { get; set; } = "زاهي";

    public string PlatformLegalNameEn { get; set; } = "Zahy Platform";

    public string PlatformLegalNameAr { get; set; } = "منصة زاهي";

    /// <summary>
    /// Beta posture: while true (default), every generated document carries a prominent
    /// "BETA — not a valid tax invoice" banner so trial documents can't be mistaken for
    /// cleared ZATCA tax invoices. Set Finance:Branding:BetaMode=false only after CTO +
    /// accountant sign-off and real ZATCA clearance is wired.
    /// </summary>
    public bool BetaMode { get; set; } = true;

    public string BetaBannerEn { get; set; } = "BETA TEST DOCUMENT - NOT A VALID TAX INVOICE";

    public string BetaBannerAr { get; set; } = "مستند تجريبي - ليست فاتورة ضريبية معتمدة";

    /// <summary>Optional path to a PNG logo embedded in generated PDFs. Empty = no logo (default).</summary>
    public string LogoPngPath { get; set; } = string.Empty;
}

public static class FinanceDocumentFileNames
{
    public static string BuildExportFileName(Guid entityId, FinanceExportFormat format) =>
        format switch
        {
            FinanceExportFormat.Xlsx => $"finance-export-{entityId:N}.xlsx",
            _ => $"finance-export-{entityId:N}.csv"
        };

    public static string BuildStatementFileName(Guid entityId) =>
        $"finance-statement-{entityId:N}.pdf";

    public static string BuildInvoiceFileName(Guid entityId) =>
        $"finance-invoice-{entityId:N}.pdf";
}
