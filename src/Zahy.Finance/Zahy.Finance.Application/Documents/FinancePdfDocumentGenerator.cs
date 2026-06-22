using System.IO;
using System.Linq;
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
                    RenderLogo(branding, column);
                    column.Item().Text(branding.BrandName).Bold().FontSize(20);
                    column.Item().Text(branding.BrandNameArabic).FontSize(16);
                    column.Item().PaddingTop(8).Text("Account Statement / كشف حساب").Bold();
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    RenderBetaBanner(branding, column);

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
                    RenderLogo(branding, column);
                    RenderBetaBanner(branding, column);
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

    /// <summary>
    /// Renders a MANUAL (ad-hoc) invoice from its keyed-in line items — NOT from the ledger snapshot.
    /// Same BETA badge + ZATCA shaping + KYC seller block as the ledger-sourced path, with a line-item
    /// table, subtotal (ex VAT), VAT, grand total, recipient block, and notes.
    /// </summary>
    public static byte[] GenerateManualInvoice(
        FinanceBrandingOptions branding,
        FinanceDocument document,
        FinanceDocumentKycBlockDto verifiedRecipient,
        ZatcaFatooraInvoiceDto zatca)
    {
        var lines = document.Lines.OrderBy(l => l.LineNo).ToList();
        var subtotalNet = lines.Sum(l => l.VatNet);
        var vatTotal = lines.Sum(l => l.VatAmount);
        var grandTotal = lines.Sum(l => l.LineTotalInclusive);
        var currency = document.Currency;

        var pdfDocument = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Column(column =>
                {
                    RenderLogo(branding, column);
                    RenderBetaBanner(branding, column);
                    column.Item().Text(branding.BrandName).Bold().FontSize(20);
                    column.Item().Text(branding.BrandNameArabic);
                    column.Item().PaddingTop(8).Text("Tax Invoice / فاتورة ضريبية").Bold();
                    column.Item().Text($"Invoice No: {document.InvoiceNumber}");
                    column.Item().Text($"UUID: {zatca.Uuid}");
                    column.Item().Text($"Issue Date: {document.GeneratedAt:yyyy-MM-dd}");

                    column.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Column(sellerCol =>
                        {
                            sellerCol.Item().Text("Seller / البائع").Bold();
                            sellerCol.Item().Text(branding.PlatformLegalNameEn);
                            sellerCol.Item().Text(branding.PlatformLegalNameAr);
                        });

                        row.RelativeItem().Column(buyerCol =>
                        {
                            buyerCol.Item().AlignRight().Text("Bill To / إلى").Bold();
                            buyerCol.Item().AlignRight().Text(document.Recipient ?? string.Empty);
                            buyerCol.Item().AlignRight().Text($"({document.RecipientType})");
                        });
                    });

                    column.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("#").Bold();
                            header.Cell().Text("Description").Bold();
                            header.Cell().AlignRight().Text("Qty").Bold();
                            header.Cell().AlignRight().Text("Unit (Incl.)").Bold();
                            header.Cell().AlignRight().Text("Line Total").Bold();
                            header.Cell().AlignRight().Text("VAT").Bold();
                            header.Cell().AlignRight().Text("Total (Incl.)").Bold();
                        });

                        foreach (var line in lines)
                        {
                            table.Cell().Text(line.LineNo.ToString());
                            table.Cell().Text(line.Description);
                            table.Cell().AlignRight().Text(line.Quantity.ToString("0.####"));
                            table.Cell().AlignRight().Text(line.UnitPriceInclusive.ToString("0.00"));
                            table.Cell().AlignRight().Text(line.VatNet.ToString("0.00"));
                            table.Cell().AlignRight().Text(line.VatAmount.ToString("0.00"));
                            table.Cell().AlignRight().Text(line.LineTotalInclusive.ToString("0.00"));
                        }
                    });

                    column.Item().PaddingTop(12).AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"Subtotal (ex VAT): {subtotalNet:0.00} {currency}");
                        totals.Item().Text($"VAT (15%): {vatTotal:0.00} {currency}");
                        totals.Item().Text($"Grand Total (Incl.): {grandTotal:0.00} {currency}").Bold();
                    });

                    if (!string.IsNullOrWhiteSpace(document.Notes))
                    {
                        column.Item().PaddingTop(16).Text("Notes / ملاحظات").Bold();
                        column.Item().Text(document.Notes);
                    }

                    column.Item().PaddingTop(12).Text("Verified Recipient / المستلم المعتمد").Bold();
                    column.Item().Text(verifiedRecipient.LegalNameEn);
                    column.Item().Text(verifiedRecipient.LegalNameAr);
                    column.Item().Text($"CR: {verifiedRecipient.CommercialRegistrationNumber}");
                    column.Item().Text($"VAT: {verifiedRecipient.VatNumber}");
                    column.Item().PaddingTop(8).Text($"QR Placeholder: {zatca.QrCodePlaceholder}");
                });
            });
        });

        return pdfDocument.GeneratePdf();
    }

    private static void RenderBetaBanner(FinanceBrandingOptions branding, QuestPDF.Fluent.ColumnDescriptor column)
    {
        if (!branding.BetaMode)
        {
            return;
        }

        column.Item()
            .PaddingBottom(10)
            .Background(Colors.Orange.Lighten4)
            .Border(1)
            .BorderColor(Colors.Orange.Darken1)
            .Padding(8)
            .Column(banner =>
            {
                banner.Item().Text(branding.BetaBannerEn).Bold().FontColor(Colors.Orange.Darken3);
                banner.Item().Text(branding.BetaBannerAr).FontColor(Colors.Orange.Darken3);
            });
    }

    private static void RenderLogo(FinanceBrandingOptions branding, QuestPDF.Fluent.ColumnDescriptor column)
    {
        if (string.IsNullOrWhiteSpace(branding.LogoPngPath))
        {
            return;
        }

        try
        {
            if (!File.Exists(branding.LogoPngPath))
            {
                return;
            }

            var bytes = File.ReadAllBytes(branding.LogoPngPath);
            column.Item().PaddingBottom(6).MaxHeight(48).Image(bytes);
        }
        catch
        {
            // Logo is decorative — never fail a financial document over branding.
        }
    }
}

public static class FinancePdfTextExtractor
{
    public static string ExtractSearchableText(byte[] pdfBytes) =>
        System.Text.Encoding.Latin1.GetString(pdfBytes);
}
