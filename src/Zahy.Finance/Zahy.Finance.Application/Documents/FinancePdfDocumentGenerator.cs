using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Zahy.Finance;

public static class FinancePdfDocumentGenerator
{
    static FinancePdfDocumentGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] GenerateStatement(
        FinanceBrandingOptions branding,
        FinancePostingLedgerSnapshot ledger,
        FinanceDocumentKycBlockDto verifiedKyc,
        string accountLabelEn,
        string accountLabelAr)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text(branding.BrandName).Bold().FontSize(20);
                    column.Item().Text(branding.BrandNameArabic).FontSize(16);
                    column.Item().PaddingTop(8).Text("Account Statement / كشف حساب").Bold();
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(accountLabelEn).Bold();
                            left.Item().Text(accountLabelAr);
                            left.Item().Text($"Account ID: {ledger.AccountId:N}");
                        });

                        row.RelativeItem().Column(right =>
                        {
                            right.Item().AlignRight().Text("Verified KYC / بيانات معتمدة").Bold();
                            right.Item().AlignRight().Text(verifiedKyc.LegalNameEn);
                            right.Item().AlignRight().Text(verifiedKyc.LegalNameAr);
                            right.Item().AlignRight().Text($"CR: {verifiedKyc.CommercialRegistrationNumber}");
                            right.Item().AlignRight().Text($"VAT: {verifiedKyc.VatNumber}");
                        });
                    });

                    column.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Posted At").Bold();
                            header.Cell().Text("Source").Bold();
                            header.Cell().AlignRight().Text("Amount (SAR)").Bold();
                        });

                        foreach (var posting in ledger.Postings)
                        {
                            table.Cell().Text(posting.PostedAt.ToString("u"));
                            table.Cell().Text($"{posting.SourceModule}:{posting.SourceType}");
                            table.Cell().AlignRight().Text(posting.PostingAmount.ToString("0.00"));
                        }

                        table.Cell().ColumnSpan(2).Text("Total / الإجمالي").Bold();
                        table.Cell().AlignRight().Text(ledger.PostingSum.ToString("0.00")).Bold();
                    });
                });

                page.Footer().AlignCenter().Text($"{branding.PlatformLegalNameEn} | {branding.PlatformLegalNameAr}");
            });
        });

        return document.GeneratePdf();
    }

    public static byte[] GenerateInvoice(
        FinanceBrandingOptions branding,
        FinancePostingLedgerSnapshot ledger,
        FinanceDocumentKycBlockDto verifiedKyc,
        ZatcaFatooraInvoiceDto zatca)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);

                page.Content().Column(column =>
                {
                    column.Item().Text(branding.BrandName).Bold().FontSize(20);
                    column.Item().Text(branding.BrandNameArabic);
                    column.Item().PaddingTop(8).Text("Tax Invoice / فاتورة ضريبية").Bold();
                    column.Item().Text($"Invoice No: {zatca.InvoiceNumber}");
                    column.Item().Text($"UUID: {zatca.Uuid}");
                    column.Item().PaddingTop(12).Text("Verified Seller / البائع المعتمد").Bold();
                    column.Item().Text(verifiedKyc.LegalNameEn);
                    column.Item().Text(verifiedKyc.LegalNameAr);
                    column.Item().Text($"CR: {verifiedKyc.CommercialRegistrationNumber}");
                    column.Item().Text($"VAT: {verifiedKyc.VatNumber}");
                    column.Item().PaddingTop(12).Text($"Tax Exclusive: {zatca.TaxExclusiveAmount:0.00} {zatca.Currency}");
                    column.Item().Text($"Tax Amount: {zatca.TaxAmount:0.00} {zatca.Currency}");
                    column.Item().Text($"Tax Inclusive: {zatca.TaxInclusiveAmount:0.00} {zatca.Currency}");
                    column.Item().Text($"Posting Total: {ledger.PostingSum:0.00} {ledger.Currency}").Bold();
                    column.Item().PaddingTop(8).Text($"QR Placeholder: {zatca.QrCodePlaceholder}");
                });
            });
        });

        return document.GeneratePdf();
    }
}

public static class FinancePdfTextExtractor
{
    public static string ExtractSearchableText(byte[] pdfBytes) =>
        System.Text.Encoding.Latin1.GetString(pdfBytes);
}
