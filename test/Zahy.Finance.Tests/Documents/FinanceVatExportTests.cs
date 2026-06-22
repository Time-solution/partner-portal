using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Shouldly;
using Xunit;
using Zahy.Settlement;

namespace Zahy.Finance;

/// <summary>
/// Per-transaction VAT export. The row logic + generators are proven here against journal entries
/// derived from the Settlement posting templates (the only source carrying VAT account codes), so the
/// export's Output − Input equals <see cref="SettlementReports.VatControl"/> for the same period.
/// The production journal provider is a documented seam (returns empty until posted journals exist).
/// </summary>
public class FinanceVatExportTests
{
    private const decimal VatRate = 0.15m;
    private static readonly DateTime June = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 7);

    private static readonly Guid PartnerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PartnerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MerchantA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MerchantB = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void VatExport_Includes_OutputVat_From_PrincipalSale()
    {
        var rows = FinanceVatExportRowBuilder.Build(PrincipalEntries(PartnerA, MerchantA, "ORD-1"));

        var output = rows.Single(r => r.VatAccountCode == FinanceVatAccountCodes.OutputVat);
        output.VatDirection.ShouldBe("Cr");
        output.VatAmount.ShouldBe(13.04m);
        output.CounterAccountCode.ShouldBe(SettlementAccountCode.ResaleRevenue); // 4100
        output.CounterAmount.ShouldBe(86.96m);
        output.RelatedDocumentRef.ShouldBe("ORD-1");
    }

    [Fact]
    public void VatExport_Includes_InputVat_From_PartnerBuy()
    {
        var rows = FinanceVatExportRowBuilder.Build(PrincipalEntries(PartnerA, MerchantA, "ORD-1"));

        var input = rows.Single(r => r.VatAccountCode == FinanceVatAccountCodes.InputVat);
        input.VatDirection.ShouldBe("Dr");
        input.VatAmount.ShouldBe(9.13m);
        input.CounterAccountCode.ShouldBe(SettlementAccountCode.PartnerCogs); // 5100
        input.CounterAmount.ShouldBe(60.87m);
    }

    [Fact]
    public void VatExport_Excludes_NonVat_Postings()
    {
        // A payment receipt (Dr 1100 / Cr 1200) carries no VAT line — it must not appear in the export.
        var paymentEntry = new FinanceVatJournalEntry
        {
            PostedAt = June,
            SourceModule = "Settlement",
            SourceType = "PaymentReceived",
            SourceId = "PMT-1",
            JournalEntryId = Guid.NewGuid(),
            Lines = new[]
            {
                Line(SettlementAccountCode.BankCashClearing, "Bank / Cash Clearing", FinanceVatEntryDirection.Debit, 100m),
                Line(SettlementAccountCode.ArMerchant, "AR-Merchant", FinanceVatEntryDirection.Credit, 100m)
            }
        };

        var rows = FinanceVatExportRowBuilder.Build(new[] { paymentEntry });

        rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task VatExport_PlatformWide_When_No_Filter()
    {
        var entries = PrincipalEntries(PartnerA, MerchantA, "ORD-A")
            .Concat(PrincipalEntries(PartnerB, MerchantB, "ORD-B"))
            .ToList();
        var sut = new FinanceVatExportAppService(new FakeVatJournalProvider(entries));

        var rows = await sut.BuildRowsAsync(new FinanceVatExportRequest());

        rows.Select(r => r.RelatedPartnerId).Distinct().ShouldBe(new Guid?[] { PartnerA, PartnerB }, ignoreOrder: true);
        rows.Count.ShouldBe(4); // 2 VAT lines (output + input) per principal case × 2 cases
    }

    [Fact]
    public async Task VatExport_FilteredByPartner_OnlyPartnerRows()
    {
        var entries = PrincipalEntries(PartnerA, MerchantA, "ORD-A")
            .Concat(PrincipalEntries(PartnerB, MerchantB, "ORD-B"))
            .ToList();
        var sut = new FinanceVatExportAppService(new FakeVatJournalProvider(entries));

        var rows = await sut.BuildRowsAsync(new FinanceVatExportRequest { PartnerId = PartnerA });

        rows.ShouldAllBe(r => r.RelatedPartnerId == PartnerA);
        rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task VatExport_FilteredByMerchant_OnlyMerchantRows()
    {
        var entries = PrincipalEntries(PartnerA, MerchantA, "ORD-A")
            .Concat(PrincipalEntries(PartnerB, MerchantB, "ORD-B"))
            .ToList();
        var sut = new FinanceVatExportAppService(new FakeVatJournalProvider(entries));

        var rows = await sut.BuildRowsAsync(new FinanceVatExportRequest { MerchantId = MerchantB });

        rows.ShouldAllBe(r => r.RelatedMerchantId == MerchantB);
        rows.Count.ShouldBe(2);
    }

    [Fact]
    public void VatExport_DateRange_BoundariesInclusive()
    {
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        var entries = new[]
        {
            OneOutputEntry(from, "ON-FROM"),
            OneOutputEntry(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc), "INSIDE"),
            OneOutputEntry(to, "ON-TO"),
            OneOutputEntry(new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc), "BEFORE"),
            OneOutputEntry(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "AFTER")
        };

        var rows = FinanceVatExportRowBuilder.Build(entries, from, to);

        rows.Select(r => r.RelatedDocumentRef).ShouldBe(new[] { "ON-FROM", "INSIDE", "ON-TO" }, ignoreOrder: true);
    }

    [Fact]
    public void VatExport_RoundPerLine_NetsToVatControl()
    {
        var principal = SettlementPostingTemplates.Principal(
            Money.Of(100m, "SAR", vatInclusive: true),
            Money.Of(70m, "SAR", vatInclusive: true),
            VatRate);

        var rows = FinanceVatExportRowBuilder.Build(Decompose(principal, June, "PrincipalCase", "ORD-1", PartnerA, MerchantA, "ORD-1"));

        var exportOutput = rows.Where(r => r.VatAccountCode == FinanceVatAccountCodes.OutputVat).Sum(r => r.VatAmount);
        var exportInput = rows.Where(r => r.VatAccountCode == FinanceVatAccountCodes.InputVat).Sum(r => r.VatAmount);
        var exportNet = FinanceMoney.RoundPosting(exportOutput - exportInput);

        var vatControl = SettlementReports.VatControl(
            new[] { principal.Tag(PartnerA, MerchantA, Period, "ORD-1") },
            Period);

        exportNet.ShouldBe(3.91m);
        exportNet.ShouldBe(vatControl.NetVatPayable.Amount);
    }

    [Fact]
    public void VatExport_Csv_HasExpectedHeaders()
    {
        var rows = FinanceVatExportRowBuilder.Build(PrincipalEntries(PartnerA, MerchantA, "ORD-1"));

        var csv = System.Text.Encoding.UTF8.GetString(FinanceVatCsvExportGenerator.Generate(rows));
        var header = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).First().TrimEnd('\r');

        header.ShouldBe(string.Join(",", FinanceVatCsvExportGenerator.Headers));
        header.ShouldContain("VatAccountCode");
        header.ShouldContain("CounterAccountCode");
    }

    [Fact]
    public void VatExport_Xlsx_HasExpectedHeaders_And_RowCount()
    {
        var rows = FinanceVatExportRowBuilder.Build(PrincipalEntries(PartnerA, MerchantA, "ORD-1"));

        var bytes = FinanceVatExcelExportGenerator.Generate(rows);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);

        for (var c = 0; c < FinanceVatExcelExportGenerator.Headers.Count; c++)
        {
            sheet.Cell(1, c + 1).GetString().ShouldBe(FinanceVatExcelExportGenerator.Headers[c]);
        }

        var lastRow = sheet.LastRowUsed()!.RowNumber();
        (lastRow - 1).ShouldBe(rows.Count); // data rows excluding header
    }

    // --- helpers -----------------------------------------------------------------------------------

    private static IReadOnlyList<FinanceVatJournalEntry> PrincipalEntries(Guid partnerId, Guid merchantId, string orderRef)
    {
        var principal = SettlementPostingTemplates.Principal(
            Money.Of(100m, "SAR", vatInclusive: true),
            Money.Of(70m, "SAR", vatInclusive: true),
            VatRate);

        return Decompose(principal, June, "PrincipalCase", orderRef, partnerId, merchantId, orderRef);
    }

    private static FinanceVatJournalEntry OneOutputEntry(DateTime postedAt, string docRef) =>
        new()
        {
            PostedAt = postedAt,
            SourceModule = "Settlement",
            SourceType = "FeeCase",
            SourceId = docRef,
            JournalEntryId = Guid.NewGuid(),
            RelatedPartnerId = PartnerA,
            RelatedMerchantId = MerchantA,
            RelatedDocumentRef = docRef,
            Lines = new[]
            {
                Line(SettlementAccountCode.OutputVat, "Output VAT", FinanceVatEntryDirection.Credit, 13.04m),
                Line(SettlementAccountCode.FeeRevenue, "Fee Revenue", FinanceVatEntryDirection.Credit, 86.96m)
            }
        };

    /// <summary>
    /// Splits a settlement journal into one VAT entry per VAT line, pairing each VAT line with its
    /// taxable-base counter-leg (output VAT ↔ revenue, input VAT ↔ COGS) — exactly what a future
    /// Settlement-backed provider would do.
    /// </summary>
    private static IReadOnlyList<FinanceVatJournalEntry> Decompose(
        PostingResult result,
        DateTime postedAt,
        string sourceType,
        string sourceId,
        Guid? partnerId,
        Guid? merchantId,
        string? docRef)
    {
        var entries = new List<FinanceVatJournalEntry>();

        foreach (var vatLine in result.Lines.Where(l => FinanceVatAccountCodes.IsVat(l.AccountCode)))
        {
            var baseCode = BaseCodeFor(vatLine.AccountCode, result);
            var baseLine = result.Lines.First(l => l.AccountCode == baseCode);

            entries.Add(new FinanceVatJournalEntry
            {
                PostedAt = postedAt,
                SourceModule = "Settlement",
                SourceType = sourceType,
                SourceId = sourceId,
                JournalEntryId = Guid.NewGuid(),
                RelatedPartnerId = partnerId,
                RelatedMerchantId = merchantId,
                RelatedDocumentRef = docRef,
                Lines = new[] { ToLine(vatLine), ToLine(baseLine) }
            });
        }

        return entries;
    }

    private static string BaseCodeFor(string vatCode, PostingResult result)
    {
        if (vatCode == SettlementAccountCode.InputVat)
        {
            return SettlementAccountCode.PartnerCogs; // 5100
        }

        // Output VAT pairs with the revenue line present on the entry (resale 4100 or fee 4200).
        return result.Lines.Any(l => l.AccountCode == SettlementAccountCode.ResaleRevenue)
            ? SettlementAccountCode.ResaleRevenue
            : SettlementAccountCode.FeeRevenue;
    }

    private static FinanceVatJournalLine ToLine(PostingLine line) =>
        Line(
            line.AccountCode,
            AccountName(line.AccountCode),
            line.Direction == EntryDirection.Debit ? FinanceVatEntryDirection.Debit : FinanceVatEntryDirection.Credit,
            line.Amount.Amount);

    private static FinanceVatJournalLine Line(string code, string name, FinanceVatEntryDirection direction, decimal amount) =>
        new() { AccountCode = code, AccountName = name, Direction = direction, Amount = amount };

    private static string AccountName(string code) => code switch
    {
        "1100" => "Bank / Cash Clearing",
        "1200" => "AR-Merchant",
        "1300" => "Input VAT",
        "2100" => "AP-Partner",
        "2200" => "Output VAT",
        "4100" => "Resale Revenue",
        "4200" => "Fee Revenue",
        "5100" => "Partner COGS",
        _ => code
    };

    private sealed class FakeVatJournalProvider : IFinanceVatJournalProvider
    {
        private readonly IReadOnlyList<FinanceVatJournalEntry> _entries;

        public FakeVatJournalProvider(IReadOnlyList<FinanceVatJournalEntry> entries) => _entries = entries;

        public Task<IReadOnlyList<FinanceVatJournalEntry>> GetVatJournalEntriesAsync(
            DateTime? from,
            DateTime? to,
            Guid? partnerId,
            Guid? merchantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries);
    }
}
