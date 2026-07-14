using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>Input for one billing line. One per active service/subscription (multi-service supported).</summary>
public abstract record InvoiceLineInput(string ServiceType, BillingType BillingType);

/// <summary>
/// A recurring monthly subscription line. <see cref="ActiveDays"/> = null means the full month;
/// otherwise the inclusive amount is PRORATED by actual calendar days of the period's month.
/// </summary>
public sealed record SubscriptionLineInput(string ServiceType, Money MonthlyInclusive, int? ActiveDays = null)
    : InvoiceLineInput(ServiceType, BillingType.Subscription);

/// <summary>A per-successful-transaction line: <see cref="SuccessfulCount"/> × the per-txn fee.</summary>
public sealed record PerTransactionLineInput(string ServiceType, Money PerTxnInclusive, int SuccessfulCount)
    : InvoiceLineInput(ServiceType, BillingType.PerTransaction);

/// <summary>One rendered billing line: inclusive + the ex-VAT / VAT split (round-per-line).</summary>
public sealed record InvoiceLine(
    string ServiceType,
    BillingType BillingType,
    SettlementPeriod Cycle,
    string QtyOrBasis,
    Money AmountInclusive,
    Money ExVat,
    Money Vat);

/// <summary>
/// DIRECTION 1 — an INVOICE (the entity owes Zahy / a receivable). One document per entity+period with
/// one line per active service, combined totals, and payment status read from <see cref="PaymentLedger"/>.
/// Reads existing read models (bulk-invoice / fee config / payment ledger) and reuses <see cref="VatMath"/>
/// for the split — it recomputes no money. BETA-stamped: NOT a ZATCA-compliant tax invoice yet. Never
/// merged with the partner settlement statement (Direction 2).
/// </summary>
public sealed record InvoiceReport(
    InvoiceEntityType EntityType,
    Guid EntityId,
    string EntityCode,
    SettlementPeriod Period,
    string InvoiceNumber,
    IReadOnlyList<InvoiceLine> Lines,
    Money TotalInclusive,
    Money TotalExVat,
    Money TotalVat,
    Money PaidToDate,
    Money Remaining,
    PaymentState State)
{
    /// <summary>Always true at this phase — every generated invoice is BETA until ZATCA.</summary>
    public bool IsBeta => true;

    public string BetaLabel => SettlementInvoiceConsts.BetaLabel;
}

/// <summary>
/// Builds the <see cref="InvoiceReport"/> read model. No money is recomputed: line amounts come from
/// the configured fees / bulk-invoice counts, the VAT split is the single <see cref="VatMath"/> helper,
/// and paid/remaining/state come straight from <see cref="PaymentLedger"/>.
/// </summary>
public static class InvoiceReader
{
    public static InvoiceReport Build(
        InvoiceEntityType entityType,
        Guid entityId,
        string entityCode,
        SettlementPeriod period,
        IEnumerable<InvoiceLineInput> lineInputs,
        IEnumerable<Payment> payments,
        decimal vatRate = ActivationFeeComputer.DefaultVatRate,
        int sequence = 1)
    {
        Check.NotNull(period, nameof(period));
        var inputs = lineInputs?.ToList() ?? new List<InvoiceLineInput>();

        var invoiceNumber = SettlementInvoiceNumber.For(entityCode, period, sequence);
        var daysInMonth = DateTime.DaysInMonth(period.Year, period.Month);

        var lines = inputs.Select(input => BuildLine(input, period, daysInMonth, vatRate)).ToList();

        var currency = lines.Count > 0 ? lines[0].AmountInclusive.Currency : SettlementConsts.DefaultCurrency;

        // Round-per-line policy: totals are the SUM of the per-line splits.
        var totalInclusive = SettlementMoney.Round(lines.Sum(l => l.AmountInclusive.Amount));
        var totalExVat = SettlementMoney.Round(lines.Sum(l => l.ExVat.Amount));
        var totalVat = SettlementMoney.Round(lines.Sum(l => l.Vat.Amount));

        // Payment status read from the ledger (against this invoice number) — no recompute.
        var status = PaymentLedger.Status(
            invoiceNumber,
            Money.Of(totalInclusive, currency, vatInclusive: true),
            payments ?? Enumerable.Empty<Payment>());

        return new InvoiceReport(
            entityType,
            entityId,
            entityCode.Trim().ToUpperInvariant(),
            period,
            invoiceNumber,
            lines,
            Money.Of(totalInclusive, currency, vatInclusive: true),
            Money.Of(totalExVat, currency),
            Money.Of(totalVat, currency),
            status.PaidToDate,
            status.Remaining,
            status.State);
    }

    private static InvoiceLine BuildLine(InvoiceLineInput input, SettlementPeriod period, int daysInMonth, decimal vatRate)
    {
        return input switch
        {
            SubscriptionLineInput s => SubscriptionLine(s, period, daysInMonth, vatRate),
            PerTransactionLineInput p => PerTransactionLine(p, period, vatRate),
            _ => throw new AbpException($"Unknown invoice line input '{input.GetType().Name}'."),
        };
    }

    private static InvoiceLine SubscriptionLine(SubscriptionLineInput s, SettlementPeriod period, int daysInMonth, decimal vatRate)
    {
        var activeDays = s.ActiveDays ?? daysInMonth;
        if (activeDays < 0 || activeDays > daysInMonth)
        {
            throw new AbpException($"Active days {activeDays} out of range for {daysInMonth}-day month.");
        }

        var currency = s.MonthlyInclusive.Currency;

        // Prorate the INCLUSIVE amount by actual calendar days (shared locked rule), THEN split (round-per-line).
        var prorated = SettlementProration.ProrateByCalendarDays(s.MonthlyInclusive.Amount, activeDays, daysInMonth);

        var (exVat, vat) = Split(prorated, vatRate);

        var basis = activeDays >= daysInMonth
            ? $"full month ({daysInMonth}/{daysInMonth} days)"
            : $"{activeDays}/{daysInMonth} days";

        return new InvoiceLine(
            s.ServiceType,
            BillingType.Subscription,
            period,
            basis,
            Money.Of(prorated, currency, vatInclusive: true),
            Money.Of(exVat, currency),
            Money.Of(vat, currency));
    }

    private static InvoiceLine PerTransactionLine(PerTransactionLineInput p, SettlementPeriod period, decimal vatRate)
    {
        var count = Math.Max(0, p.SuccessfulCount);
        var currency = p.PerTxnInclusive.Currency;

        var lineInclusive = SettlementMoney.Round(count * p.PerTxnInclusive.Amount);
        var (exVat, vat) = Split(lineInclusive, vatRate);

        var basis = string.Format(
            CultureInfo.InvariantCulture,
            "{0} × {1:0.00} {2}",
            count,
            p.PerTxnInclusive.Amount,
            currency);

        return new InvoiceLine(
            p.ServiceType,
            BillingType.PerTransaction,
            period,
            basis,
            Money.Of(lineInclusive, currency, vatInclusive: true),
            Money.Of(exVat, currency),
            Money.Of(vat, currency));
    }

    private static (decimal ExVat, decimal Vat) Split(decimal inclusive, decimal vatRate)
    {
        var exVat = VatMath.NetOfInclusive(inclusive, vatRate);
        var vat = SettlementMoney.Round(inclusive - exVat);
        return (exVat, vat);
    }
}
