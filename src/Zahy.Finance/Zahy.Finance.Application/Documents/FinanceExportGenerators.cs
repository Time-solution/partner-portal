using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;

namespace Zahy.Finance;

public static class FinanceCsvExportGenerator
{
    public static byte[] Generate(FinancePostingLedgerSnapshot ledger)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PostedAt,SourceModule,SourceType,SourceId,PostingAmount,Currency,Description");

        foreach (var row in ledger.Postings)
        {
            builder.Append(CsvEscape(row.PostedAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(CsvEscape(row.SourceModule.ToString()));
            builder.Append(',');
            builder.Append(CsvEscape(row.SourceType));
            builder.Append(',');
            builder.Append(CsvEscape(row.SourceId));
            builder.Append(',');
            builder.Append(row.PostingAmount.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(ledger.Currency));
            builder.Append(',');
            builder.AppendLine(CsvEscape(row.Description ?? string.Empty));
        }

        builder.Append(",,,Total,");
        builder.Append(ledger.PostingSum.ToString("0.00", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.AppendLine(CsvEscape(ledger.Currency));

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public static class FinanceExcelExportGenerator
{
    public static byte[] Generate(FinancePostingLedgerSnapshot ledger)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Postings");
        sheet.Cell(1, 1).Value = "PostedAt";
        sheet.Cell(1, 2).Value = "SourceModule";
        sheet.Cell(1, 3).Value = "SourceType";
        sheet.Cell(1, 4).Value = "SourceId";
        sheet.Cell(1, 5).Value = "PostingAmount";
        sheet.Cell(1, 6).Value = "Currency";
        sheet.Cell(1, 7).Value = "Description";

        var rowIndex = 2;
        foreach (var row in ledger.Postings)
        {
            sheet.Cell(rowIndex, 1).Value = row.PostedAt;
            sheet.Cell(rowIndex, 2).Value = row.SourceModule.ToString();
            sheet.Cell(rowIndex, 3).Value = row.SourceType;
            sheet.Cell(rowIndex, 4).Value = row.SourceId;
            sheet.Cell(rowIndex, 5).Value = row.PostingAmount;
            sheet.Cell(rowIndex, 6).Value = ledger.Currency;
            sheet.Cell(rowIndex, 7).Value = row.Description ?? string.Empty;
            rowIndex++;
        }

        sheet.Cell(rowIndex, 4).Value = "Total";
        sheet.Cell(rowIndex, 5).Value = ledger.PostingSum;
        sheet.Cell(rowIndex, 6).Value = ledger.Currency;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

/// <summary>
/// Per-transaction VAT export (CSV). One row per VAT journal line × counter-leg, showing
/// source → destination. Reuses the same CSV escaping / UTF-8 pipeline as the postings exporter;
/// the postings exporter above is untouched.
/// </summary>
public static class FinanceVatCsvExportGenerator
{
    public static readonly IReadOnlyList<string> Headers = new[]
    {
        "PostedAt",
        "SourceModule",
        "SourceType",
        "SourceId",
        "JournalEntryId",
        "VatAccountCode",
        "VatAccountName",
        "VatDirection",
        "VatAmount",
        "CounterAccountCode",
        "CounterAccountName",
        "CounterAmount",
        "RelatedPartnerId",
        "RelatedMerchantId",
        "RelatedDocumentRef"
    };

    public static byte[] Generate(IReadOnlyList<FinanceVatExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", Headers));

        foreach (var row in rows)
        {
            builder.Append(CsvEscape(row.PostedAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(CsvEscape(row.SourceModule));
            builder.Append(',');
            builder.Append(CsvEscape(row.SourceType));
            builder.Append(',');
            builder.Append(CsvEscape(row.SourceId));
            builder.Append(',');
            builder.Append(CsvEscape(row.JournalEntryId.ToString()));
            builder.Append(',');
            builder.Append(CsvEscape(row.VatAccountCode));
            builder.Append(',');
            builder.Append(CsvEscape(row.VatAccountName));
            builder.Append(',');
            builder.Append(CsvEscape(row.VatDirection));
            builder.Append(',');
            builder.Append(row.VatAmount.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(row.CounterAccountCode));
            builder.Append(',');
            builder.Append(CsvEscape(row.CounterAccountName));
            builder.Append(',');
            builder.Append(row.CounterAmount.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(row.RelatedPartnerId?.ToString() ?? string.Empty));
            builder.Append(',');
            builder.Append(CsvEscape(row.RelatedMerchantId?.ToString() ?? string.Empty));
            builder.Append(',');
            builder.AppendLine(CsvEscape(row.RelatedDocumentRef ?? string.Empty));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

/// <summary>Per-transaction VAT export (XLSX). Same row shape as the CSV exporter.</summary>
public static class FinanceVatExcelExportGenerator
{
    public static IReadOnlyList<string> Headers => FinanceVatCsvExportGenerator.Headers;

    public static byte[] Generate(IReadOnlyList<FinanceVatExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("VatTransactions");

        for (var c = 0; c < Headers.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = Headers[c];
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.PostedAt;
            sheet.Cell(rowIndex, 2).Value = row.SourceModule;
            sheet.Cell(rowIndex, 3).Value = row.SourceType;
            sheet.Cell(rowIndex, 4).Value = row.SourceId;
            sheet.Cell(rowIndex, 5).Value = row.JournalEntryId.ToString();
            sheet.Cell(rowIndex, 6).Value = row.VatAccountCode;
            sheet.Cell(rowIndex, 7).Value = row.VatAccountName;
            sheet.Cell(rowIndex, 8).Value = row.VatDirection;
            sheet.Cell(rowIndex, 9).Value = row.VatAmount;
            sheet.Cell(rowIndex, 10).Value = row.CounterAccountCode;
            sheet.Cell(rowIndex, 11).Value = row.CounterAccountName;
            sheet.Cell(rowIndex, 12).Value = row.CounterAmount;
            sheet.Cell(rowIndex, 13).Value = row.RelatedPartnerId?.ToString() ?? string.Empty;
            sheet.Cell(rowIndex, 14).Value = row.RelatedMerchantId?.ToString() ?? string.Empty;
            sheet.Cell(rowIndex, 15).Value = row.RelatedDocumentRef ?? string.Empty;
            rowIndex++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

public static class FinanceExportTotalParser
{
    public static decimal ReadCsvTotal(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content);
        var totalLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Last(line => line.Contains("Total", StringComparison.Ordinal));

        var parts = totalLine.Split(',');
        return decimal.Parse(parts[4], CultureInfo.InvariantCulture);
    }

    public static decimal ReadExcelTotal(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var lastRow = sheet.LastRowUsed()!.RowNumber();
        return Convert.ToDecimal(sheet.Cell(lastRow, 5).GetDouble());
    }
}
