using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

/// <summary>
/// Per-transaction VAT export request. Usable by Accountant / PlatformAdmin only (Finance.ReadAll).
/// PartnerId / MerchantId are optional filters — when both are null the export is platform-wide.
/// </summary>
public sealed class FinanceVatExportRequest
{
    public FinanceExportFormat Format { get; init; } = FinanceExportFormat.Csv;

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public Guid? PartnerId { get; init; }

    public Guid? MerchantId { get; init; }
}

public enum FinanceVatEntryDirection
{
    Debit = 1,
    Credit = 2
}

/// <summary>
/// The chart codes that mark a journal line as a VAT line for export purposes. These mirror the
/// Settlement chart (1300 Input VAT, 2200 Output VAT, 2300 VAT Control) but are kept here as plain
/// strings so the Finance contracts/export stay decoupled from the Settlement assembly.
/// </summary>
public static class FinanceVatAccountCodes
{
    public const string InputVat = "1300";

    public const string OutputVat = "2200";

    public const string VatControl = "2300";

    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        InputVat,
        OutputVat,
        VatControl
    };

    public static bool IsVat(string? code) => code != null && Codes.Contains(code.Trim());
}

/// <summary>
/// One journal line crossing into the VAT export. <see cref="AccountName"/> is supplied by the
/// data source (resolved from the chart of accounts) so the export never re-derives names.
/// </summary>
public sealed class FinanceVatJournalLine
{
    public string AccountCode { get; init; } = string.Empty;

    public string AccountName { get; init; } = string.Empty;

    public FinanceVatEntryDirection Direction { get; init; }

    public decimal Amount { get; init; }
}

/// <summary>
/// A single balanced VAT-bearing journal event sourced for the export. Each entry is expected to be
/// one taxable event (e.g. the sell side or the buy side of a principal case) so that every VAT line
/// pairs cleanly with its taxable-base counter-leg(s). Amounts are already rounded to the money policy.
/// </summary>
public sealed class FinanceVatJournalEntry
{
    public DateTime PostedAt { get; init; }

    public string SourceModule { get; init; } = string.Empty;

    public string SourceType { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public Guid JournalEntryId { get; init; }

    public Guid? RelatedPartnerId { get; init; }

    public Guid? RelatedMerchantId { get; init; }

    public string? RelatedDocumentRef { get; init; }

    public IReadOnlyList<FinanceVatJournalLine> Lines { get; init; } = Array.Empty<FinanceVatJournalLine>();
}

/// <summary>One export row: a VAT line paired with one of its counter-legs (source → destination).</summary>
public sealed class FinanceVatExportRow
{
    public DateTime PostedAt { get; init; }

    public string SourceModule { get; init; } = string.Empty;

    public string SourceType { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public Guid JournalEntryId { get; init; }

    public string VatAccountCode { get; init; } = string.Empty;

    public string VatAccountName { get; init; } = string.Empty;

    public string VatDirection { get; init; } = string.Empty;

    public decimal VatAmount { get; init; }

    public string CounterAccountCode { get; init; } = string.Empty;

    public string CounterAccountName { get; init; } = string.Empty;

    public decimal CounterAmount { get; init; }

    public Guid? RelatedPartnerId { get; init; }

    public Guid? RelatedMerchantId { get; init; }

    public string? RelatedDocumentRef { get; init; }
}

/// <summary>
/// SEAM: supplies the VAT-bearing journal entries for the export over a date range / optional scope.
/// There is no persisted double-entry GL with VAT account codes today (settlement posting is
/// compute-only / flagged OFF, the Finance posting ledger stores single net amounts without codes),
/// so the default production implementation returns nothing. A Settlement-backed implementation can
/// be wired here once posted journals exist — exactly like the other compute-only seams in the platform.
/// </summary>
public interface IFinanceVatJournalProvider
{
    Task<IReadOnlyList<FinanceVatJournalEntry>> GetVatJournalEntriesAsync(
        DateTime? from,
        DateTime? to,
        Guid? partnerId,
        Guid? merchantId,
        CancellationToken cancellationToken = default);
}

public interface IFinanceVatExportAppService
{
    Task<FinanceDocumentBytesResult> ExportVatTransactionsAsync(
        FinanceVatExportRequest request,
        CancellationToken cancellationToken = default);
}
