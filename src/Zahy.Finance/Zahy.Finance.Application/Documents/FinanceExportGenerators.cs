using System;
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
