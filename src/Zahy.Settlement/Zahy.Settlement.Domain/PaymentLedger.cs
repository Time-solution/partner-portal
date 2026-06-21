using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Payment status of one AR position (a balance owed to Zahy), against a settlement/invoice ref.
/// <see cref="Remaining"/> is never negative (overpayment is rejected before it can arise) and the
/// <see cref="State"/> reflects the Allocated → PartiallyPaid → Paid progression.
/// </summary>
public sealed record PaymentStatusReport(
    string AgainstRef,
    Money ArTotal,
    Money PaidToDate,
    Money Remaining,
    PaymentState State);

/// <summary>
/// Pure read model over a set of <see cref="Payment"/> receipts for a single AR position. Supports
/// PARTIAL and MULTIPLE payments against one balance: <see cref="PaidToDate"/> sums every receipt for
/// the ref, <see cref="Status"/> derives remaining + state, and <see cref="EnsureWithinRemaining"/>
/// rejects any receipt that would exceed the outstanding balance (overpayment). Record/compute only —
/// nothing is posted or disbursed here.
/// </summary>
public static class PaymentLedger
{
    /// <summary>Cumulative received for <paramref name="againstRef"/> across all matching payments.</summary>
    public static decimal PaidToDate(string againstRef, IEnumerable<Payment> payments)
    {
        var key = Normalize(againstRef);
        return SettlementMoney.Round(
            payments.Where(p => Normalize(p.AgainstRef) == key).Sum(p => p.Amount));
    }

    /// <summary>
    /// Snapshot for one ref: AR total, paid-to-date, remaining (= total − paid, never negative) and
    /// the derived <see cref="PaymentState"/>. Raises <see cref="SettlementPaymentErrorCodes.Overpayment"/>
    /// if the recorded payments already exceed the AR total (a ledger that was never guarded).
    /// </summary>
    public static PaymentStatusReport Status(string againstRef, Money arTotal, IEnumerable<Payment> payments)
    {
        Check.NotNull(arTotal, nameof(arTotal));

        var paid = PaidToDate(againstRef, payments);
        var total = SettlementMoney.Round(arTotal.Amount);

        if (paid > total)
        {
            throw new BusinessException(SettlementPaymentErrorCodes.Overpayment)
                .WithData("AgainstRef", againstRef)
                .WithData("ArTotal", total)
                .WithData("PaidToDate", paid);
        }

        var remaining = SettlementMoney.Round(total - paid);
        var currency = arTotal.Currency;

        return new PaymentStatusReport(
            Normalize(againstRef),
            Money.Of(total, currency),
            Money.Of(paid, currency),
            Money.Of(remaining, currency),
            StateOf(paid, total));
    }

    /// <summary>
    /// Guards a NEW receipt before it is recorded: throws
    /// <see cref="SettlementPaymentErrorCodes.Overpayment"/> when paid-to-date + the new amount would
    /// exceed the AR total. Use this prior to <see cref="Payment.Record"/> to keep the ledger valid.
    /// </summary>
    public static void EnsureWithinRemaining(Money arTotal, decimal paidToDate, Money newPayment)
    {
        Check.NotNull(arTotal, nameof(arTotal));
        Check.NotNull(newPayment, nameof(newPayment));

        if (!string.Equals(arTotal.Currency, newPayment.Currency, StringComparison.Ordinal))
        {
            throw new BusinessException(SettlementPaymentErrorCodes.PaymentCurrencyMismatch)
                .WithData("ArTotal", arTotal.Currency)
                .WithData("Payment", newPayment.Currency);
        }

        var prospective = SettlementMoney.Round(SettlementMoney.Round(paidToDate) + newPayment.Amount);
        if (prospective > SettlementMoney.Round(arTotal.Amount))
        {
            throw new BusinessException(SettlementPaymentErrorCodes.Overpayment)
                .WithData("ArTotal", arTotal.Amount)
                .WithData("PaidToDate", SettlementMoney.Round(paidToDate))
                .WithData("Attempted", newPayment.Amount);
        }
    }

    private static PaymentState StateOf(decimal paid, decimal total)
    {
        if (paid <= 0m)
        {
            return PaymentState.Allocated;
        }

        return paid >= total ? PaymentState.Paid : PaymentState.PartiallyPaid;
    }

    private static string Normalize(string againstRef) =>
        (againstRef ?? string.Empty).Trim().ToLowerInvariant();
}
