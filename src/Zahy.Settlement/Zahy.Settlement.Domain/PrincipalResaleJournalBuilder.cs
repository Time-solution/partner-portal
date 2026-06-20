using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Builds a balanced principal-resale journal from a <see cref="CostMarkupLine"/> and
/// <see cref="ResaleVatCalculator"/> output. Round-per-line throughout (accountant-approved).
/// ⚠️ PROVISIONAL leg mapping — pending chart sign-off (DESIGN.md §11.6). No disbursement.
/// </summary>
public static class PrincipalResaleJournalBuilder
{
    public static Journal Build(
        CostMarkupLine line,
        ResaleVatResult vat,
        SettlementBook book,
        DateTime postedAt,
        Guid journalId,
        string? reference = null)
    {
        Check.NotNull(line, nameof(line));
        Check.NotNull(vat, nameof(vat));

        if (vat.Treatment != VatTreatment.Principal)
        {
            throw new BusinessException(SettlementVatErrorCodes.UnknownVatTreatment)
                .WithData("Treatment", vat.Treatment.ToString());
        }

        var lines = new List<JournalLine>(capacity: 5);
        var currency = line.Currency;
        var sellInclusive = line.SellPrice.Amount;
        var buyInclusive = line.BuyPrice.Amount;

        if (book == SettlementBook.Marketplace)
        {
            lines.Add(JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(sellInclusive, currency)));
            lines.Add(JournalLine.Debit(SettlementAccountType.VatInput, vat.InputVat));
            lines.Add(JournalLine.Credit(SettlementAccountType.DeliveryCost, Money.Of(buyInclusive, currency)));
            lines.Add(JournalLine.Credit(SettlementAccountType.ShippingMarginRevenue, vat.Margin));
            lines.Add(JournalLine.Credit(SettlementAccountType.VatOutput, vat.OutputVat));
        }
        else if (book == SettlementBook.Integration)
        {
            lines.Add(JournalLine.Debit(SettlementAccountType.PartnerPayable, Money.Of(sellInclusive, currency)));
            lines.Add(JournalLine.Debit(SettlementAccountType.VatInput, vat.InputVat));
            lines.Add(JournalLine.Credit(SettlementAccountType.PartnerPayable, Money.Of(buyInclusive, currency)));
            lines.Add(JournalLine.Credit(SettlementAccountType.ShippingMarginRevenue, vat.Margin));
            lines.Add(JournalLine.Credit(SettlementAccountType.VatOutput, vat.OutputVat));
        }
        else
        {
            throw new BusinessException(SettlementWebhookErrorCodes.AllocationNotConfiguredForBook)
                .WithData("Book", book.ToString());
        }

        return Journal.Create(journalId, postedAt, lines, reference);
    }
}
