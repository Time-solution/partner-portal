namespace Zahy.Finance;

public class FinanceBrandingOptions
{
    public const string SectionName = "Finance:Branding";

    public string BrandName { get; set; } = "Zahy";

    public string BrandNameArabic { get; set; } = "زاهي";

    public string PlatformLegalNameEn { get; set; } = "Zahy Platform";

    public string PlatformLegalNameAr { get; set; } = "منصة زاهي";
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
