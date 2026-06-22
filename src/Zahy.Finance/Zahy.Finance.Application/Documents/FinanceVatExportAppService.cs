using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Zahy.Identity.Permissions;

namespace Zahy.Finance;

/// <summary>
/// Per-transaction VAT export for Accountant / PlatformAdmin (Finance.ReadAll). Pulls VAT-bearing
/// journal entries from <see cref="IFinanceVatJournalProvider"/>, builds source → destination rows
/// and renders CSV / XLSX. Read-only; nothing is posted and no flag is consulted.
/// </summary>
[Authorize(ZahyPermissions.Finance.ReadAll)]
public class FinanceVatExportAppService : ApplicationService, IFinanceVatExportAppService
{
    private const string CsvContentType = "text/csv";

    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IFinanceVatJournalProvider _journalProvider;

    public FinanceVatExportAppService(IFinanceVatJournalProvider journalProvider)
    {
        _journalProvider = journalProvider;
    }

    public async Task<FinanceDocumentBytesResult> ExportVatTransactionsAsync(
        FinanceVatExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildRowsAsync(request, cancellationToken);

        byte[] content;
        string contentType;
        string extension;

        if (request.Format == FinanceExportFormat.Xlsx)
        {
            content = FinanceVatExcelExportGenerator.Generate(rows);
            contentType = XlsxContentType;
            extension = "xlsx";
        }
        else
        {
            content = FinanceVatCsvExportGenerator.Generate(rows);
            contentType = CsvContentType;
            extension = "csv";
        }

        var netVat = NetVatPayable(rows);

        return new FinanceDocumentBytesResult
        {
            Content = content,
            ContentType = contentType,
            FileName = BuildFileName(request, extension),
            PostingSum = netVat,
            DocumentKind = FinanceDocumentKind.TransactionExport
        };
    }

    /// <summary>Provider → date/scope filter → rows. Exposed for unit tests of the filter + row shape.</summary>
    public async Task<IReadOnlyList<FinanceVatExportRow>> BuildRowsAsync(
        FinanceVatExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var entries = await _journalProvider.GetVatJournalEntriesAsync(
            request.From,
            request.To,
            request.PartnerId,
            request.MerchantId,
            cancellationToken);

        return FinanceVatExportRowBuilder.Build(
            entries,
            request.From,
            request.To,
            request.PartnerId,
            request.MerchantId);
    }

    private static decimal NetVatPayable(IEnumerable<FinanceVatExportRow> rows)
    {
        var output = 0m;
        var input = 0m;
        foreach (var row in rows)
        {
            if (row.VatAccountCode == FinanceVatAccountCodes.OutputVat)
            {
                output += row.VatAmount;
            }
            else if (row.VatAccountCode == FinanceVatAccountCodes.InputVat)
            {
                input += row.VatAmount;
            }
        }

        return FinanceMoney.RoundPosting(output - input);
    }

    private static string BuildFileName(FinanceVatExportRequest request, string extension)
    {
        var scope = request.PartnerId != null
            ? $"partner-{request.PartnerId:N}"
            : request.MerchantId != null
                ? $"merchant-{request.MerchantId:N}"
                : "platform";

        var from = request.From?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "all";
        var to = request.To?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "all";

        return $"vat-transactions-{scope}-{from}-{to}.{extension}";
    }
}

/// <summary>
/// Default production seam: returns no rows. No persisted double-entry GL with VAT account codes
/// exists yet (settlement posting is compute-only / flagged OFF; the Finance posting ledger stores
/// single net amounts without account codes), so there is nothing to read. Replace with a
/// Settlement-backed provider once posted VAT journals are persisted.
/// </summary>
public class NullFinanceVatJournalProvider : IFinanceVatJournalProvider
{
    public Task<IReadOnlyList<FinanceVatJournalEntry>> GetVatJournalEntriesAsync(
        DateTime? from,
        DateTime? to,
        Guid? partnerId,
        Guid? merchantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinanceVatJournalEntry>>(Array.Empty<FinanceVatJournalEntry>());
}
