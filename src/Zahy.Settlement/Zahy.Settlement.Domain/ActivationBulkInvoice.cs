using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>
/// A transaction that MAY be billed a per-transaction activation fee. Only those with
/// <see cref="Successful"/> = true are billed (the success flag is the single gate).
/// </summary>
public sealed record FeeBillableTransaction(
    Guid MerchantId,
    string MerchantName,
    string OrderRef,
    Money FeeInclusive,
    bool Successful);

/// <summary>One billed transaction line on the invoice (order ref + its VAT-inclusive fee).</summary>
public sealed record BulkInvoiceTxn(string OrderRef, Money FeeInclusive);

/// <summary>Per-merchant drill-down: the merchant's billed transactions + their inclusive subtotal.</summary>
public sealed record BulkInvoiceLine(
    Guid MerchantId,
    string MerchantName,
    IReadOnlyList<BulkInvoiceTxn> Transactions,
    Money SubtotalInclusive);

/// <summary>
/// A partner's monthly bulk invoice read model (mirrors the frontend mock <c>BulkInvoice</c>):
/// every SUCCESSFUL transaction's per-txn fee across ALL the partner's merchants, rounded per line
/// then summed, with per-merchant / per-transaction drill-down and an ex-VAT + VAT split on the total.
/// DISPLAY ONLY — building this posts no journal (<c>PostingEnabled</c> stays OFF).
/// </summary>
public sealed record BulkInvoiceReport(
    Guid PartnerId,
    string PartnerName,
    SettlementPeriod Period,
    IReadOnlyList<BulkInvoiceLine> Lines,
    int TransactionCount,
    Money TotalInclusive,
    Money TotalNet,
    Money TotalVat);

public static class ActivationBulkInvoice
{
    /// <summary>
    /// Build the partner's bulk invoice for the period from the supplied transactions. Successful
    /// transactions only; grouped per merchant; round-per-line (each fee is 2dp) then sum into the
    /// merchant subtotal and the grand total; the total is split into ex-VAT + VAT (round-per-line).
    /// </summary>
    public static BulkInvoiceReport Build(
        Guid partnerId,
        string partnerName,
        SettlementPeriod period,
        IEnumerable<FeeBillableTransaction> transactions,
        decimal vatRate = ActivationFeeComputer.DefaultVatRate)
    {
        var billable = transactions.Where(t => t.Successful).ToList();
        var currency = billable.Count > 0 ? billable[0].FeeInclusive.Currency : SettlementConsts.DefaultCurrency;

        var lines = billable
            .GroupBy(t => t.MerchantId)
            .Select(g =>
            {
                var txns = g
                    .Select(t => new BulkInvoiceTxn(t.OrderRef, t.FeeInclusive))
                    .ToList();
                var subtotal = SettlementMoney.Round(txns.Sum(x => x.FeeInclusive.Amount));
                return new BulkInvoiceLine(
                    g.Key,
                    g.First().MerchantName,
                    txns,
                    Money.Of(subtotal, currency, vatInclusive: true));
            })
            .OrderBy(l => l.MerchantName, StringComparer.Ordinal)
            .ToList();

        var totalInclusive = SettlementMoney.Round(lines.Sum(l => l.SubtotalInclusive.Amount));
        var totalNet = VatMath.NetOfInclusive(totalInclusive, vatRate);
        var totalVat = SettlementMoney.Round(totalInclusive - totalNet);

        return new BulkInvoiceReport(
            partnerId,
            partnerName,
            period,
            lines,
            billable.Count,
            Money.Of(totalInclusive, currency, vatInclusive: true),
            Money.Of(totalNet, currency),
            Money.Of(totalVat, currency));
    }
}
