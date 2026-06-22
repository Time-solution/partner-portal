using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Finance;

/// <summary>
/// Pure transform: VAT-bearing journal entries → per-transaction export rows. For each journal line
/// that touches a VAT account (1300 / 2200 / 2300) we emit one row per NON-VAT counter-leg in the same
/// entry, so "source → destination" is visible. When a VAT line has multiple counter-legs the VAT
/// amount is split pro-rata across them by counter-leg magnitude (rows share the JournalEntryId so they
/// remain groupable, and the pro-rata parts sum back to the original VAT amount via a remainder fix-up).
/// No money is invented: every amount comes straight off the supplied (already-rounded) journal lines.
/// </summary>
public static class FinanceVatExportRowBuilder
{
    public static IReadOnlyList<FinanceVatExportRow> Build(
        IEnumerable<FinanceVatJournalEntry> entries,
        DateTime? from = null,
        DateTime? to = null,
        Guid? partnerId = null,
        Guid? merchantId = null)
    {
        var rows = new List<FinanceVatExportRow>();

        foreach (var entry in entries.Where(e => Matches(e, from, to, partnerId, merchantId))
                     .OrderBy(e => e.PostedAt)
                     .ThenBy(e => e.JournalEntryId))
        {
            var vatLines = entry.Lines.Where(l => FinanceVatAccountCodes.IsVat(l.AccountCode)).ToList();
            if (vatLines.Count == 0)
            {
                continue;
            }

            var counterLines = entry.Lines.Where(l => !FinanceVatAccountCodes.IsVat(l.AccountCode)).ToList();

            foreach (var vatLine in vatLines)
            {
                rows.AddRange(RowsForVatLine(entry, vatLine, counterLines));
            }
        }

        return rows;
    }

    private static IEnumerable<FinanceVatExportRow> RowsForVatLine(
        FinanceVatJournalEntry entry,
        FinanceVatJournalLine vatLine,
        IReadOnlyList<FinanceVatJournalLine> counterLines)
    {
        var vatAmount = FinanceMoney.RoundPosting(vatLine.Amount);

        if (counterLines.Count == 0)
        {
            yield return BuildRow(entry, vatLine, vatAmount, counter: null, counterAmount: 0m);
            yield break;
        }

        if (counterLines.Count == 1)
        {
            var only = counterLines[0];
            yield return BuildRow(entry, vatLine, vatAmount, only, FinanceMoney.RoundPosting(only.Amount));
            yield break;
        }

        // Multiple counter-legs: split the VAT amount pro-rata by counter magnitude. Track the running
        // total so the LAST row absorbs any rounding remainder (the pro-rata parts net back to vatAmount).
        var totalMagnitude = counterLines.Sum(l => Math.Abs(l.Amount));
        var allocated = 0m;

        for (var i = 0; i < counterLines.Count; i++)
        {
            var counter = counterLines[i];
            decimal share;
            if (i == counterLines.Count - 1 || totalMagnitude == 0m)
            {
                share = FinanceMoney.RoundPosting(vatAmount - allocated);
            }
            else
            {
                share = FinanceMoney.RoundPosting(vatAmount * (Math.Abs(counter.Amount) / totalMagnitude));
                allocated += share;
            }

            yield return BuildRow(entry, vatLine, share, counter, FinanceMoney.RoundPosting(counter.Amount));
        }
    }

    private static FinanceVatExportRow BuildRow(
        FinanceVatJournalEntry entry,
        FinanceVatJournalLine vatLine,
        decimal vatAmount,
        FinanceVatJournalLine? counter,
        decimal counterAmount) =>
        new()
        {
            PostedAt = entry.PostedAt,
            SourceModule = entry.SourceModule,
            SourceType = entry.SourceType,
            SourceId = entry.SourceId,
            JournalEntryId = entry.JournalEntryId,
            VatAccountCode = vatLine.AccountCode,
            VatAccountName = vatLine.AccountName,
            VatDirection = DirectionLabel(vatLine.Direction),
            VatAmount = vatAmount,
            CounterAccountCode = counter?.AccountCode ?? string.Empty,
            CounterAccountName = counter?.AccountName ?? string.Empty,
            CounterAmount = counterAmount,
            RelatedPartnerId = entry.RelatedPartnerId,
            RelatedMerchantId = entry.RelatedMerchantId,
            RelatedDocumentRef = entry.RelatedDocumentRef
        };

    private static bool Matches(
        FinanceVatJournalEntry entry,
        DateTime? from,
        DateTime? to,
        Guid? partnerId,
        Guid? merchantId)
    {
        if (from != null && entry.PostedAt < from.Value)
        {
            return false;
        }

        if (to != null && entry.PostedAt > to.Value)
        {
            return false;
        }

        if (partnerId != null && entry.RelatedPartnerId != partnerId.Value)
        {
            return false;
        }

        if (merchantId != null && entry.RelatedMerchantId != merchantId.Value)
        {
            return false;
        }

        return true;
    }

    private static string DirectionLabel(FinanceVatEntryDirection direction) =>
        direction == FinanceVatEntryDirection.Debit ? "Dr" : "Cr";
}
